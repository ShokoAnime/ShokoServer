using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

/// <summary>
///   Plugin dependencies: the load order and refusals they impose, and the
///   plugins installed as dependencies, with the <c>.dependency</c> marker,
///   the enabled state that follows their dependents, and orphans.
/// </summary>
public partial class PluginManager
{
    #region Dependency Graph

    /// <summary>
    ///   Orders the plugins so every dependency loads before its dependents,
    ///   and fails every enabled plugin whose required dependencies are not
    ///   satisfied.
    /// </summary>
    /// <param name="plugins">Every registered plugin version. Reordered in place.</param>
    /// <param name="logger">The logger to write the refusals to.</param>
    internal static void ApplyDependencyGraph(List<LocalPluginInfo> plugins, ILogger logger)
        => ApplyDependencyGraph(plugins, [], logger);

    /// <summary>
    ///   Orders the plugins so every dependency loads before its dependents,
    ///   and fails every enabled plugin whose required dependencies are not
    ///   satisfied.
    /// </summary>
    /// <param name="plugins">Every registered plugin version. Reordered in place.</param>
    /// <param name="refusals">
    ///   Filled with the load state each refused plugin had before it was
    ///   refused, so <see cref="ReapplyDependencyGraph"/> can take a refusal
    ///   back.
    /// </param>
    /// <param name="logger">The logger to write the refusals to.</param>
    internal static void ApplyDependencyGraph(List<LocalPluginInfo> plugins, Dictionary<LocalPluginInfo, PluginLoadState> refusals, ILogger logger)
    {
        var rank = RefuseUnsatisfiedDependencies(plugins, refusals, logger);
        // A plugin refused at start fails it, as any enabled plugin that cannot load does.
        foreach (var pluginInfo in refusals.Keys)
            MarkFailed(pluginInfo, pluginInfo.CannotLoadReason ?? "The plugin's dependencies are not satisfied.");

        var reordered = plugins
            .Select((pluginInfo, index) => (pluginInfo, index, rank: rank.GetValueOrDefault(pluginInfo.ID)))
            .OrderBy(tuple => tuple.rank)
            .ThenBy(tuple => tuple.index)
            .Select(tuple => tuple.pluginInfo)
            .ToList();
        plugins.Clear();
        plugins.AddRange(reordered);
        // InitPlugins indexes into the list by LoadOrder, so the two must stay in sync.
        for (var i = 0; i < plugins.Count; i++)
            plugins[i].LoadOrder = i;
    }

    /// <summary>
    ///   Refuses the plugins whose required dependencies are no longer
    ///   satisfied, and takes back the refusals of those whose dependencies
    ///   are satisfied again, after a plugin was installed, enabled, disabled
    ///   or uninstalled. The next start would refuse the same plugins, so an
    ///   active plugin refused here shows it waits on a restart. The load
    ///   order is left as it is.
    /// </summary>
    /// <param name="plugins">Every registered plugin version.</param>
    /// <param name="refusals">
    ///   The load state each plugin refused so far had before it was refused.
    ///   Replaced with the refusals made now.
    /// </param>
    /// <param name="logger">The logger to write the refusals and the ones taken back to.</param>
    internal static void ReapplyDependencyGraph(IReadOnlyList<LocalPluginInfo> plugins, Dictionary<LocalPluginInfo, PluginLoadState> refusals, ILogger logger)
    {
        var before = plugins.ToDictionary(pluginInfo => pluginInfo, pluginInfo => new PluginLoadState(pluginInfo.CanLoad, pluginInfo.CannotLoadReason));
        foreach (var (pluginInfo, state) in refusals)
        {
            pluginInfo.CanLoad = state.CanLoad;
            pluginInfo.CannotLoadReason = state.CannotLoadReason;
        }

        refusals.Clear();
        RefuseUnsatisfiedDependencies(plugins, refusals, NullLogger.Instance);
        foreach (var (pluginInfo, state) in before)
        {
            if (!pluginInfo.IsEnabled || (pluginInfo.CanLoad == state.CanLoad && pluginInfo.CannotLoadReason == state.CannotLoadReason))
                continue;

            if (pluginInfo.CanLoad)
                logger.LogInformation("Plugin \"{Name}\" ({PluginID}) can load again after a restart. ({Version})", pluginInfo.Name, pluginInfo.ID, pluginInfo.Version);
            else
                logger.LogWarning(
                    "Plugin \"{Name}\" ({PluginID}) will not load after a restart: {Reason} ({Version})",
                    pluginInfo.Name,
                    pluginInfo.ID,
                    pluginInfo.CannotLoadReason,
                    pluginInfo.Version
                );
        }
    }

    /// <summary>
    ///   Refuses every enabled plugin whose required dependencies are not
    ///   satisfied, along with every plugin in or behind a dependency cycle.
    /// </summary>
    /// <param name="plugins">Every registered plugin version.</param>
    /// <param name="refusals">Filled with the load state each refused plugin had before it was refused.</param>
    /// <param name="logger">The logger to write the refusals to.</param>
    /// <returns>The load rank of each enabled plugin; a dependency always ranks below its dependents.</returns>
    private static Dictionary<Guid, int> RefuseUnsatisfiedDependencies(IReadOnlyList<LocalPluginInfo> plugins, Dictionary<LocalPluginInfo, PluginLoadState> refusals, ILogger logger)
    {
        // Several versions can be enabled at once (an update installed beside the loaded one), so
        // each plugin is judged by the version the next start picks: pinned first, then the highest.
        var known = new Dictionary<Guid, LocalPluginInfo>();
        var installed = new HashSet<Guid>();
        var enabled = new Dictionary<Guid, LocalPluginInfo>();
        var position = new Dictionary<Guid, int>();
        for (var i = 0; i < plugins.Count; i++)
        {
            var pluginInfo = plugins[i];
            known.TryAdd(pluginInfo.ID, pluginInfo);
            if (!pluginInfo.IsInstalled)
                continue;

            installed.Add(pluginInfo.ID);
            if (!pluginInfo.IsEnabled)
                continue;

            if (enabled.TryGetValue(pluginInfo.ID, out var current) && !IsPickedBefore(pluginInfo, current))
                continue;

            enabled[pluginInfo.ID] = pluginInfo;
            position[pluginInfo.ID] = i;
        }

        static bool IsPickedBefore(LocalPluginInfo candidate, LocalPluginInfo current)
            => candidate.IsPinned != current.IsPinned ? candidate.IsPinned : candidate.Version.Version > current.Version.Version;

        var inDegree = enabled.Keys.ToDictionary(id => id, _ => 0);
        var dependents = enabled.Keys.ToDictionary(id => id, _ => new List<Guid>());
        foreach (var (id, pluginInfo) in enabled)
            foreach (var dependency in pluginInfo.Dependencies)
                if (dependents.TryGetValue(dependency.PluginID, out var list) && !list.Contains(id))
                {
                    list.Add(id);
                    inDegree[id]++;
                }

        var queue = new PriorityQueue<Guid, int>();
        foreach (var (id, degree) in inDegree)
            if (degree is 0)
                queue.Enqueue(id, position[id]);

        var rank = enabled.Keys.ToDictionary(id => id, _ => 0);
        var ordered = new List<Guid>(enabled.Count);
        while (queue.TryDequeue(out var id, out _))
        {
            ordered.Add(id);
            foreach (var dependent in dependents[id])
            {
                if (rank[dependent] <= rank[id])
                    rank[dependent] = rank[id] + 1;
                if (--inDegree[dependent] is 0)
                    queue.Enqueue(dependent, position[dependent]);
            }
        }

        var refused = new HashSet<Guid>();
        string DescribeDependency(Guid id)
            => known.TryGetValue(id, out var target) ? $"\"{target.Name}\" ({id})" : id.ToString();

        // Keeps any reason given earlier, such as a runtime the plugin was not
        // built for, in front of the new one.
        void Refuse(LocalPluginInfo pluginInfo, string reason)
        {
            refusals.TryAdd(pluginInfo, new(pluginInfo.CanLoad, pluginInfo.CannotLoadReason));
            pluginInfo.CanLoad = false;
            pluginInfo.CannotLoadReason = pluginInfo.CannotLoadReason is { Length: > 0 } prior ? $"{prior} {reason}" : reason;
            refused.Add(pluginInfo.ID);
        }

        string? GetUnsatisfiedReason(PluginDependency dependency)
        {
            if (!enabled.TryGetValue(dependency.PluginID, out var target))
                return installed.Contains(dependency.PluginID) ? "is installed but disabled" : "is not installed";
            if (!PluginVersionRange.IsSatisfied(dependency.VersionRange, target.Version.Version))
                return $"is installed at version {target.Version.Version}, which does not satisfy \"{dependency.VersionRange}\"";
            if (refused.Contains(dependency.PluginID))
                return "was itself refused";
            if (!target.CanLoad)
                return "cannot be loaded";
            return null;
        }

        if (ordered.Count != enabled.Count)
        {
            var cyclic = enabled.Keys.Where(id => inDegree[id] > 0).ToList();
            var participants = string.Join(", ", cyclic.Select(id => $"\"{enabled[id].Name}\" ({id})"));
            foreach (var cyclicId in cyclic)
            {
                logger.LogWarning("Refusing to load plugin \"{Name}\" ({PluginID}) because it is part of, or behind, a dependency cycle between {Participants}.", enabled[cyclicId].Name, cyclicId, participants);
                Refuse(enabled[cyclicId], $"The plugin is part of, or behind, a dependency cycle between {participants}.");
            }
        }

        foreach (var orderedId in ordered)
        {
            var pluginInfo = enabled[orderedId];
            var reasons = new List<string>();
            foreach (var dependency in pluginInfo.Dependencies)
            {
                if (GetUnsatisfiedReason(dependency) is not { } reason)
                    continue;

                if (dependency.IsOptional)
                {
                    logger.LogInformation(
                        "Ignoring optional dependency {Dependency} of plugin \"{Name}\" ({PluginID}) because it {Reason}.",
                        DescribeDependency(dependency.PluginID),
                        pluginInfo.Name,
                        orderedId,
                        reason
                    );
                    continue;
                }

                logger.LogWarning(
                    "Refusing to load plugin \"{Name}\" ({PluginID}) because its required dependency {Dependency} {Reason}.",
                    pluginInfo.Name,
                    orderedId,
                    DescribeDependency(dependency.PluginID),
                    reason
                );
                reasons.Add($"Its required dependency {DescribeDependency(dependency.PluginID)} {reason}.");
            }

            if (reasons.Count is not 0)
                Refuse(pluginInfo, string.Join(" ", reasons));
        }

        return rank;
    }

    #endregion

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
