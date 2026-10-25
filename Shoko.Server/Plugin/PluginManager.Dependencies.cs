using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

/// <summary>
///   Plugins installed as dependencies: the <c>.dependency</c> marker, the
///   enabled state that follows their dependents, and orphans.
/// </summary>
public partial class PluginManager
{
    #region Demand

    /// <summary>
    ///   A plugin version as the demand pass sees it.
    /// </summary>
    /// <param name="ID">The plugin ID.</param>
    /// <param name="DllName">The name of the main DLL, which keys the enabled state.</param>
    /// <param name="IsDependency">Whether the version is marked as installed as a dependency.</param>
    /// <param name="IsEnabled">Whether the version is enabled, before the demand pass.</param>
    /// <param name="IsPinned">Whether the version is pinned.</param>
    /// <param name="Version">The version.</param>
    /// <param name="Dependencies">The version's dependencies.</param>
    internal readonly record struct DemandNode(
        Guid ID,
        string DllName,
        bool IsDependency,
        bool IsEnabled,
        bool IsPinned,
        Version Version,
        IReadOnlyList<PluginDependency> Dependencies
    );

    /// <summary>
    ///   Finds the plugins enabled on demand, and which of them are demanded.
    ///   A plugin is enabled on demand when every installed version is marked
    ///   as a dependency and it has no enabled state of its own. It is
    ///   demanded while an enabled plugin, or a demanded one, depends on it,
    ///   required or optional, going by the version each would load.
    /// </summary>
    /// <param name="nodes">Every installed plugin version.</param>
    /// <param name="enabledStates">The enabled state of each plugin, by DLL name.</param>
    /// <returns>The plugins enabled on demand, and the ones of them demanded.</returns>
    internal static (IReadOnlySet<Guid> OnDemand, IReadOnlySet<Guid> Demanded) FindDemand(
        IReadOnlyList<DemandNode> nodes,
        IReadOnlyDictionary<string, bool> enabledStates
    )
    {
        var byID = nodes
            .GroupBy(node => node.ID)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(node => node.IsPinned).ThenByDescending(node => node.Version).ToList());
        var onDemand = byID
            .Where(pair => pair.Key != CorePlugin.StaticID && pair.Value.All(node => node.IsDependency && !enabledStates.ContainsKey(node.DllName)))
            .Select(pair => pair.Key)
            .ToHashSet();

        // Start from the version each enabled plugin would load, then follow what they depend on.
        var demanded = new HashSet<Guid>();
        var pending = new Queue<DemandNode>();
        foreach (var (id, versions) in byID)
        {
            if (onDemand.Contains(id))
                continue;

            var index = versions.FindIndex(node => node.IsEnabled);
            if (index is not -1)
                pending.Enqueue(versions[index]);
        }

        while (pending.TryDequeue(out var node))
        {
            foreach (var dependency in node.Dependencies)
            {
                if (!onDemand.Contains(dependency.PluginID) || !demanded.Add(dependency.PluginID))
                    continue;

                pending.Enqueue(byID[dependency.PluginID][0]);
            }
        }

        return (onDemand, demanded);
    }

    /// <summary>
    ///   Enables the plugin versions found on disk that are demanded, and
    ///   disables the other versions enabled on demand, before one version of
    ///   each plugin is picked to load.
    /// </summary>
    /// <param name="plugins">Every plugin version found on disk.</param>
    /// <param name="enabledStates">The enabled state of each plugin, by DLL name.</param>
    internal static void ApplyDemand(IReadOnlyList<InternalPluginInfo> plugins, IReadOnlyDictionary<string, bool> enabledStates)
    {
        var (onDemand, demanded) = FindDemand(
            plugins.Select(info => new DemandNode(info.ID, info.DllName, info.IsDependency, info.IsEnabled, info.IsPinned, info.Version.Version, info.Dependencies)).ToList(),
            enabledStates
        );
        foreach (var info in plugins)
        {
            info.IsEnabledOnDemand = onDemand.Contains(info.ID);
            if (info.IsEnabledOnDemand)
                info.IsEnabled = demanded.Contains(info.ID);
        }
    }

    /// <summary>
    ///   Brings the enabled state of the registered plugins enabled on demand
    ///   in line with what the next start would enable, after a plugin was
    ///   installed, enabled, disabled, marked or uninstalled.
    /// </summary>
    private void ReapplyDemand()
    {
        var enabledStates = ISettingsProvider.Instance.GetSettings().Plugins.EnabledPlugins;
        var installed = _pluginTypes.Where(info => info.IsInstalled).ToList();
        var (onDemand, demanded) = FindDemand(
            installed.Select(info => new DemandNode(info.ID, GetDllName(info), info.IsDependency, info.IsEnabled, info.IsPinned, info.Version.Version, info.Dependencies)).ToList(),
            enabledStates
        );
        foreach (var info in installed)
        {
            info.IsEnabledOnDemand = onDemand.Contains(info.ID);
            if (info.IsEnabledOnDemand)
                info.IsEnabled = demanded.Contains(info.ID);
        }
    }

    private static string GetDllName(LocalPluginInfo pluginInfo)
        => Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);

    #endregion

    #region Markers

    /// <summary>
    ///   What a <c>.dependency</c> file holds: why the version was installed.
    ///   Only whether the file exists drives what the server does.
    /// </summary>
    internal sealed class DependencyMarker
    {
        /// <summary>
        ///   The plugins that needed the version when it was installed. Empty
        ///   when it was marked by hand.
        /// </summary>
        [JsonProperty("requiredBy")]
        public List<Guid> RequiredBy { get; set; } = [];

        /// <summary>
        ///   When the version was marked, in UTC.
        /// </summary>
        [JsonProperty("markedAt")]
        public DateTime MarkedAt { get; set; }
    }

    /// <summary>
    ///   Writes the <c>.dependency</c> file of a plugin version.
    /// </summary>
    /// <param name="directory">The plugin's directory, if it has one.</param>
    /// <param name="dll">The plugin's main DLL.</param>
    /// <param name="requiredBy">The plugins that need the version.</param>
    internal static void WriteDependencyMarker(string? directory, string dll, IEnumerable<Guid> requiredBy)
        => File.WriteAllText(
            GetDependencyFile(directory, dll),
            JsonConvert.SerializeObject(new DependencyMarker { RequiredBy = [.. requiredBy], MarkedAt = DateTime.UtcNow })
        );

    /// <summary>
    ///   Removes the <c>.dependency</c> file of a plugin version, if it has one.
    /// </summary>
    /// <param name="directory">The plugin's directory, if it has one.</param>
    /// <param name="dll">The plugin's main DLL.</param>
    internal static void RemoveDependencyMarker(string? directory, string dll)
    {
        var path = GetDependencyFile(directory, dll);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <inheritdoc/>
    public LocalPluginInfo MarkAsDependency(LocalPluginInfo pluginInfo)
        => WithStateChanged(ToggleDependencyMarker(pluginInfo, true));

    /// <inheritdoc/>
    public LocalPluginInfo UnmarkAsDependency(LocalPluginInfo pluginInfo)
        => WithStateChanged(ToggleDependencyMarker(pluginInfo, false));

    /// <summary>
    ///   Writes or removes the <c>.dependency</c> file of a plugin version.
    ///   Marking the last unmarked version drops an enabled state of
    ///   <c>true</c>, which the scan adds to every plugin installed on
    ///   purpose, so the plugin follows its dependents; a disabled state is
    ///   the admin's and stays. Unmarking a plugin enabled on demand gives it
    ///   the state it has now.
    /// </summary>
    /// <param name="pluginInfo">The plugin version.</param>
    /// <param name="marked">Whether to mark it.</param>
    /// <returns>The plugin version.</returns>
    private LocalPluginInfo ToggleDependencyMarker(LocalPluginInfo pluginInfo, bool marked)
    {
        if (!pluginInfo.CanUninstall || !pluginInfo.IsInstalled || pluginInfo.IsDependency == marked)
            return pluginInfo;

        lock (_pluginTypes)
        {
            var dllName = GetDllName(pluginInfo);
            var settings = ISettingsProvider.Instance.GetSettings();
            if (marked)
            {
                WriteDependencyMarker(pluginInfo.ContainingDirectory, pluginInfo.DLLs[0], []);
                pluginInfo.IsDependency = true;
                var allMarked = _pluginTypes.Where(info => info.ID == pluginInfo.ID && info.IsInstalled).All(info => info.IsDependency);
                if (allMarked && settings.Plugins.EnabledPlugins.TryGetValue(dllName, out var enabled) && enabled)
                {
                    settings.Plugins.EnabledPlugins.Remove(dllName);
                    ISettingsProvider.Instance.SaveSettings(settings);
                }
            }
            else
            {
                RemoveDependencyMarker(pluginInfo.ContainingDirectory, pluginInfo.DLLs[0]);
                pluginInfo.IsDependency = false;
                if (pluginInfo.IsEnabledOnDemand && settings.Plugins.EnabledPlugins.TryAdd(dllName, pluginInfo.IsEnabled))
                    ISettingsProvider.Instance.SaveSettings(settings);
            }
        }

        logger.LogInformation(
            marked ? "Marked version {Version} of plugin \"{Name}\" as installed as a dependency." : "Marked version {Version} of plugin \"{Name}\" as installed on purpose.",
            pluginInfo.Version.Version,
            pluginInfo.Name
        );
        return pluginInfo;
    }

    /// <inheritdoc/>
    public bool IsInstalledAsDependency(Guid pluginID)
    {
        lock (_pluginTypes)
        {
            var installed = _pluginTypes.Where(info => info.ID == pluginID && info.IsInstalled).ToList();
            return installed.Count > 0 && installed.All(info => info.IsDependency);
        }
    }

    #endregion

    #region Orphans

    /// <summary>
    ///   Finds the orphaned plugins: those installed as dependencies that no
    ///   plugin installed on purpose needs, directly or through other
    ///   dependencies, required or optional.
    /// </summary>
    /// <param name="installed">Every installed plugin version.</param>
    /// <returns>The IDs of the orphaned plugins.</returns>
    internal static IReadOnlySet<Guid> FindOrphans(IReadOnlyList<LocalPluginInfo> installed)
    {
        var byID = installed.GroupBy(info => info.ID).ToDictionary(group => group.Key, group => group.ToList());
        var transient = byID.Where(pair => pair.Value.All(info => info.IsDependency)).Select(pair => pair.Key).ToHashSet();

        var needed = new HashSet<Guid>();
        var pending = new Queue<Guid>(byID.Keys.Where(id => !transient.Contains(id)));
        while (pending.TryDequeue(out var id))
        {
            foreach (var dependency in byID[id].SelectMany(info => info.Dependencies))
                if (transient.Contains(dependency.PluginID) && needed.Add(dependency.PluginID))
                    pending.Enqueue(dependency.PluginID);
        }

        transient.ExceptWith(needed);
        return transient;
    }

    /// <inheritdoc/>
    public IReadOnlyList<LocalPluginInfo> GetOrphanedPlugins()
    {
        lock (_pluginTypes)
        {
            var installed = _pluginTypes.Where(info => info.IsInstalled).ToList();
            var orphans = FindOrphans(installed);
            return installed.Where(info => orphans.Contains(info.ID)).ToList();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<LocalPluginInfo> UninstallOrphanedPlugins(IReadOnlySet<Guid>? pluginIDs = null)
    {
        var uninstalled = new List<LocalPluginInfo>();
        foreach (var pluginInfo in GetOrphanedPlugins())
        {
            if ((pluginIDs is not null && !pluginIDs.Contains(pluginInfo.ID)) || pluginInfo.IsPinned || pluginInfo.IsKept || !pluginInfo.CanUninstall)
                continue;

            if (UninstallPluginInternal(pluginInfo, purgeConfiguration: false, purgeData: false).IsInstalled)
                continue;

            logger.LogInformation("Uninstalled version {Version} of plugin \"{Name}\", as nothing needs it anymore.", pluginInfo.Version.Version, pluginInfo.Name);
            uninstalled.Add(pluginInfo);
        }

        if (uninstalled.Count > 0)
            OnStateChanged();
        return uninstalled;
    }

    #endregion
}
