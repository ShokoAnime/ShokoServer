using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CS0618
namespace Shoko.Server.Plugin;

/// <summary>
///   Installing, enabling, disabling, pinning, keeping and uninstalling
///   plugins, and removing their data.
/// </summary>
public partial class PluginManager
{
    #region Plugin Management

    /// <inheritdoc/>
    public event EventHandler<PluginInstallationEventArgs>? PluginInstalled;

    /// <inheritdoc/>
    public event EventHandler<PluginInstallationEventArgs>? PluginUninstalled;

    /// <inheritdoc/>
    public event EventHandler<PluginToggledEventArgs>? PluginEnabled;

    /// <inheritdoc/>
    public event EventHandler<PluginToggledEventArgs>? PluginDisabled;

    /// <summary>
    ///   Dispatched, synchronously, after a plugin is installed, enabled,
    ///   disabled, pinned, unpinned, kept, unkept or uninstalled, so the restart reasons can
    ///   catch up with what the next start would load.
    /// </summary>
    internal event EventHandler? StateChanged;

    /// <summary>
    ///   Brings the dependency refusals in line with what the next start would
    ///   refuse, then tells the <see cref="StateChanged"/> handlers.
    /// </summary>
    private void OnStateChanged()
    {
        lock (_pluginTypes)
        {
            ReapplyDemand();
            ReapplyDependencyGraph(_pluginTypes, _dependencyRefusals, logger);
        }

        try
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A handler threw while being told the plugin state changed.");
        }
    }

    /// <inheritdoc/>
    public LocalPluginInfo? LoadFromPath(string path)
    {
        var userPluginDir = applicationPaths.PluginsPath;
        if (path.StartsWith("%PluginsPath%"))
            path = path.Replace("%PluginsPath%", userPluginDir);
        if (!Path.IsPathFullyQualified(path))
            path = Path.Combine(userPluginDir, path);
        if (!path.StartsWith(userPluginDir + Path.DirectorySeparatorChar))
            return null;

        LocalPluginInfo? pluginInfo;
        lock (_pluginTypes)
        {
            if ((pluginInfo = LoadFromPathInternal(path)) is null)
                return null;

            // Installed again before the restart that would have removed its data, so keep it.
            UnmarkPluginDataForRemoval(pluginInfo.ID);

            // Built here rather than in the task, so it carries the actor of this call.
            var eventArgs = new PluginInstallationEventArgs { Plugin = pluginInfo, OccurredAt = DateTime.UtcNow, Actor = ActorContext.CurrentActor };
            Task.Run(() => PluginInstalled?.Invoke(null, eventArgs));
        }

        OnStateChanged();
        return pluginInfo;
    }

    public LocalPluginInfo EnablePlugin(LocalPluginInfo pluginInfo)
        => WithStateChanged(TogglePluginAndNotify(pluginInfo, true));

    public LocalPluginInfo DisablePlugin(LocalPluginInfo pluginInfo)
        => WithStateChanged(TogglePluginAndNotify(pluginInfo, false));

    /// <summary>
    ///   Enables or disables the plugin, then raises <see cref="PluginDisabled"/>
    ///   for every version that went from enabled to disabled, and
    ///   <see cref="PluginEnabled"/> for every version that went the other way.
    /// </summary>
    /// <param name="pluginInfo">The plugin info to enable or disable.</param>
    /// <param name="enabled">Whether to enable it.</param>
    /// <returns>The updated <see cref="LocalPluginInfo"/> for the plugin.</returns>
    private LocalPluginInfo TogglePluginAndNotify(LocalPluginInfo pluginInfo, bool enabled)
    {
        List<LocalPluginInfo> versions;
        lock (_pluginTypes)
            versions = [.. _pluginTypes.Where(p => p.ID == pluginInfo.ID).Append(pluginInfo).Distinct()];
        var before = versions.Select(p => p.IsEnabled).ToList();

        var result = TogglePlugin(pluginInfo, enabled);

        // Built here rather than in the task, so they carry the actor of this call.
        var occurredAt = DateTime.UtcNow;
        var actor = ActorContext.CurrentActor;
        var disabled = versions.Where((p, i) => before[i] && !p.IsEnabled).Select(p => new PluginToggledEventArgs { Plugin = p, OccurredAt = occurredAt, Actor = actor }).ToList();
        var enabledNow = versions.Where((p, i) => !before[i] && p.IsEnabled).Select(p => new PluginToggledEventArgs { Plugin = p, OccurredAt = occurredAt, Actor = actor }).ToList();
        if (disabled.Count > 0 || enabledNow.Count > 0)
        {
            Task.Run(() =>
            {
                foreach (var eventArgs in disabled)
                    PluginDisabled?.Invoke(null, eventArgs);
                foreach (var eventArgs in enabledNow)
                    PluginEnabled?.Invoke(null, eventArgs);
            });
        }

        return result;
    }

    public LocalPluginInfo PinPlugin(LocalPluginInfo pluginInfo)
        => WithStateChanged(TogglePluginPin(pluginInfo, true));

    public LocalPluginInfo UnpinPlugin(LocalPluginInfo pluginInfo)
        => WithStateChanged(TogglePluginPin(pluginInfo, false));

    public LocalPluginInfo KeepPlugin(LocalPluginInfo pluginInfo)
        => WithStateChanged(TogglePluginKeep(pluginInfo, true));

    public LocalPluginInfo UnkeepPlugin(LocalPluginInfo pluginInfo)
        => WithStateChanged(TogglePluginKeep(pluginInfo, false));

    private LocalPluginInfo WithStateChanged(LocalPluginInfo pluginInfo)
    {
        OnStateChanged();
        return pluginInfo;
    }

    /// <summary>
    ///   Raised with a plugin's ID when it is uninstalled with its data, once
    ///   no other installed version uses that data, so the core's own records
    ///   of the plugin can go with it.
    /// </summary>
    internal event EventHandler<Guid>? PluginDataPurged;

    public LocalPluginInfo UninstallPlugin(LocalPluginInfo pluginInfo, bool purgeConfiguration = false, bool purgeData = false)
        => WithStateChanged(UninstallPluginInternal(pluginInfo, purgeConfiguration || purgeData, purgeData));

    private LocalPluginInfo UninstallPluginInternal(LocalPluginInfo pluginInfo, bool purgeConfiguration, bool purgeData)
    {
        if (!pluginInfo.CanUninstall || !pluginInfo.IsInstalled)
            return pluginInfo;

        lock (_pluginTypes)
        {
            if (!pluginInfo.CanUninstall || !pluginInfo.IsInstalled)
                return pluginInfo;

            // Mark the plugin for removal upon next startup.
            if (!string.IsNullOrEmpty(pluginInfo.ContainingDirectory))
            {
                if (Directory.Exists(pluginInfo.ContainingDirectory))
                {
                    var removalFile = Path.Join(pluginInfo.ContainingDirectory, Remove);
                    var pinnedFile = Path.Join(pluginInfo.ContainingDirectory, Pinned);
                    if (!File.Exists(removalFile))
                        File.WriteAllText(removalFile, string.Empty);
                    if (File.Exists(pinnedFile))
                        File.Delete(pinnedFile);
                }
            }
            else if (pluginInfo.DLLs.Count is 1)
            {
                var removalFile = Path.ChangeExtension(pluginInfo.DLLs[0], Remove);
                var pinnedFile = Path.ChangeExtension(pluginInfo.DLLs[0], Pinned);
                if (File.Exists(pluginInfo.DLLs[0]) && !File.Exists(removalFile))
                    File.WriteAllText(removalFile, string.Empty);
                if (File.Exists(pinnedFile))
                    File.Delete(pinnedFile);
            }

            // Disable it and marked it as not installed.
            pluginInfo.UninstalledAt = DateTime.UtcNow;
            pluginInfo.IsEnabled = false;

            // Remove it from the enabled plugins dictionary.
            var dllName = Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);
            var settings = ISettingsProvider.Instance.GetSettings();
            if (settings.Plugins.EnabledPlugins.Remove(dllName))
                ISettingsProvider.Instance.SaveSettings(settings);

            // Purge the databases and the cache if requested, once no other version is left to use
            // them. The plugin is still running, so they go on the next start.
            if (purgeData)
            {
                if (_pluginTypes.Any(other => other != pluginInfo && other.ID == pluginInfo.ID && other.IsInstalled))
                    logger.LogInformation("Keeping the data of plugin \"{Name}\", as another installed version still uses it.", pluginInfo.Name);
                else
                {
                    MarkPluginDataForRemoval(pluginInfo.ID);
                    PluginDataPurged?.Invoke(this, pluginInfo.ID);
                }
            }

            // Purge configuration if requested.
            if (purgeConfiguration)
            {
                // Remove the default plugin config directory if it exists.
                var pluginConfigDir = PluginPathRules.GetConfigurationsPath(applicationPaths, pluginInfo.ID);
                if (Directory.Exists(pluginConfigDir))
                    Directory.Delete(pluginConfigDir, true);

                // Remove any configuration files outside the default plugin config directory if we have the plugin loaded.
                if (pluginInfo.Plugin is not null)
                {
                    pluginConfigDir += Path.DirectorySeparatorChar;
                    var configurationService = ISystemService.StaticServices.GetRequiredService<IConfigurationService>();
                    var configInfos = configurationService.GetConfigurationInfo(pluginInfo.Plugin);
                    foreach (var configInfo in configInfos)
                    {
                        if (string.IsNullOrEmpty(configInfo.Path) || configInfo.Path.StartsWith(pluginConfigDir))
                            continue;

                        if (File.Exists(configInfo.Path))
                            File.Delete(configInfo.Path);
                    }
                }
            }

            // Built here rather than in the task, so it carries the actor of this call.
            var eventArgs = new PluginInstallationEventArgs { Plugin = pluginInfo, OccurredAt = DateTime.UtcNow, Actor = ActorContext.CurrentActor };
            Task.Run(() => PluginUninstalled?.Invoke(null, eventArgs));

            return pluginInfo;
        }
    }

    #region Plugin Management | Data Removal

    /// <summary>
    /// The plugin data folders a removal marker can be left in: its databases and its cache.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The folders.</returns>
    private string[] GetPluginDataFolders(Guid pluginID)
        => [PluginPathRules.GetDatabasePath(applicationPaths, pluginID), PluginPathRules.GetCachePath(applicationPaths, pluginID)];

    /// <summary>
    /// Marks a plugin's databases and cache for removal on the next start.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    private void MarkPluginDataForRemoval(Guid pluginID)
    {
        // On MySQL or SQL Server the plugin's tables live in the core's database, and the marker
        // in its database folder is what finds them, so the folder is made if the plugin never did.
        if (ISettingsProvider.Instance.GetSettings().Database.Type is not Constants.DatabaseType.SQLite)
            Directory.CreateDirectory(PluginPathRules.GetDatabasePath(applicationPaths, pluginID));

        foreach (var folder in GetPluginDataFolders(pluginID))
        {
            if (!Directory.Exists(folder))
                continue;

            var removalFile = Path.Join(folder, Remove);
            if (!File.Exists(removalFile))
                File.WriteAllText(removalFile, string.Empty);
        }
    }

    /// <summary>
    /// Takes back the removal of a plugin's databases and cache.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    private void UnmarkPluginDataForRemoval(Guid pluginID)
    {
        foreach (var folder in GetPluginDataFolders(pluginID))
        {
            var removalFile = Path.Join(folder, Remove);
            if (File.Exists(removalFile))
                File.Delete(removalFile);
        }
    }

    /// <summary>
    /// Removes the plugin database and cache folders marked for removal when their plugin was
    /// uninstalled, and, while the core runs on MySQL or SQL Server, the plugin's tables in the
    /// core's database. A database folder whose tables could not be dropped is kept, so the next
    /// start tries again.
    /// </summary>
    internal void RemovePluginDataMarkedForRemoval()
    {
        foreach (var root in new[] { applicationPaths.DatabasePath, applicationPaths.CachePath })
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;

            foreach (var folder in Directory.GetDirectories(root))
            {
                if (!File.Exists(Path.Join(folder, Remove)))
                    continue;

                if (root == applicationPaths.DatabasePath && Guid.TryParse(Path.GetFileName(folder), out var pluginID) && !DropPluginTables(pluginID))
                    continue;

                logger.LogInformation("Removing the data of an uninstalled plugin: {Path}", folder);
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to remove the data of an uninstalled plugin: {Path}", folder);
                }
            }
        }
    }

    /// <summary>
    /// Drops an uninstalled plugin's tables from the core's database, when the core runs on MySQL
    /// or SQL Server.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns><c>true</c> when nothing is left of the plugin in the core's database.</returns>
    private bool DropPluginTables(Guid pluginID)
    {
        var (type, connectionString) = PluginDatabaseServer.FromSettings(ISettingsProvider.Instance.GetSettings().Database);
        if (type is Constants.DatabaseType.SQLite)
            return true;

        try
        {
            var dropped = PluginDatabaseServer.DropTables(type, connectionString, PluginTableNaming.GetPluginPrefix(pluginID));
            if (dropped.Count > 0)
                logger.LogInformation("Dropped {Count} tables of an uninstalled plugin ({PluginID}) from the core's database: {Tables}", dropped.Count, pluginID, string.Join(", ", dropped));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to drop the tables of an uninstalled plugin ({PluginID}) from the core's database; trying again on the next start.", pluginID);
            return false;
        }
    }

    #endregion

    private LocalPluginInfo? LoadFromPathInternal(string path)
    {
        if (Directory.Exists(path))
        {
            var dlls = Directory.GetFiles(path, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System });
            return LoadFromDirectoryOrDLL(path, dlls);
        }

        if (!path.EndsWith(".dll") || !File.Exists(path))
            return null;

        return LoadFromDirectoryOrDLL(null, [path]);
    }

    private LocalPluginInfo? LoadFromDirectoryOrDLL(string? containingDirectory, string[] dlls)
    {
        LocalPluginInfo? existingPluginInfo = null;
        if (!string.IsNullOrEmpty(containingDirectory))
        {
            existingPluginInfo = _pluginTypes.Find(p => p.ContainingDirectory == containingDirectory);
            if (existingPluginInfo?.IsInstalled ?? false)
                return existingPluginInfo;
        }
        else if (dlls.Length is 1)
        {
            existingPluginInfo = _pluginTypes.Find(p => p.IsInstalled && p.DLLs[0] == dlls[0]);
            if (existingPluginInfo?.IsInstalled ?? false)
                return existingPluginInfo;
        }
        else
        {
            return null;
        }

        if (LoadInternalPluginInfo(containingDirectory, dlls) is not { } internalPluginInfo)
            return null;

        var pluginInfo = new LocalPluginInfo()
        {
            ID = internalPluginInfo.ID,
            Name = internalPluginInfo.Name,
            Description = internalPluginInfo.Description,
            Version = internalPluginInfo.Version,
            Authors = internalPluginInfo.Authors,
            RepositoryUrl = internalPluginInfo.RepositoryUrl,
            HomepageUrl = internalPluginInfo.HomepageUrl,
            Tags = internalPluginInfo.Tags,
            LoadOrder = _pluginTypes.Count,
            InstalledAt = internalPluginInfo.InstalledAt,
            IsKept = internalPluginInfo.IsKept,
            IsDependency = internalPluginInfo.IsDependency,
            IsEnabledOnDemand = internalPluginInfo.IsEnabledOnDemand,
            IsEnabled = internalPluginInfo.IsEnabled,
            IsPinned = internalPluginInfo.IsPinned,
            IsActive = existingPluginInfo?.IsActive ?? false,
            CanLoad = internalPluginInfo.CanLoad,
            CannotLoadReason = internalPluginInfo.CannotLoadReason,
            CanUninstall = internalPluginInfo.CanUninstall,
            Plugin = existingPluginInfo?.Plugin,
            PluginType = existingPluginInfo?.PluginType,
            ServiceRegistrationType = existingPluginInfo?.ServiceRegistrationType,
            ApplicationRegistrationType = existingPluginInfo?.ApplicationRegistrationType,
            ContainingDirectory = internalPluginInfo.ContainingDirectory,
            DLLs = internalPluginInfo.DLLs,
            Types = existingPluginInfo?.Types ?? [],
            Thumbnail = PackageImageLoader.Load(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail, ThumbnailKind, applicationPaths),
            Icon = PackageImageLoader.Load(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Icon, IconKind, applicationPaths),
            Dependencies = internalPluginInfo.Dependencies,
        };
        if (existingPluginInfo is not null && _pluginTypes.IndexOf(existingPluginInfo) is { } index && index is not -1)
            _pluginTypes[index] = pluginInfo;
        else
            _pluginTypes.Add(pluginInfo);

        // Unmark the plugin for removal when it's reinstalled again.
        if (existingPluginInfo is not null && !string.IsNullOrEmpty(pluginInfo.ContainingDirectory))
        {
            if (Directory.Exists(pluginInfo.ContainingDirectory))
            {
                var removalFile = Path.Join(pluginInfo.ContainingDirectory, Remove);
                if (File.Exists(removalFile))
                    File.Delete(removalFile);
            }
        }
        else if (existingPluginInfo is not null && pluginInfo.DLLs.Count is 1)
        {
            var removalFile = Path.ChangeExtension(pluginInfo.DLLs[0], Remove);
            if (File.Exists(removalFile))
                File.Delete(removalFile);
        }

        return pluginInfo;
    }

    private LocalPluginInfo TogglePlugin(LocalPluginInfo pluginInfo, bool enabled)
    {
        // Every provider the core itself registers -- its hash provider, its relocation
        // providers, its playback observers -- is attributed to this plugin entry, because
        // `GetPluginInfo(Assembly)` resolves them through the assembly it names. Disabling it
        // would therefore not disable one plugin; it would disable the core's participation in
        // every provider service at once. It is not a plugin in that sense and is not offered
        // as one (`showCorePlugin` defaults to false), so the toggle is refused here rather
        // than special-cased at each caller.
        if (pluginInfo.ID == CorePlugin.StaticID)
            return pluginInfo;

        var dllName = Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);
        var settings = ISettingsProvider.Instance.GetSettings();
        if (enabled)
        {
            if (!pluginInfo.IsInstalled)
                return pluginInfo;

            if ((!settings.Plugins.EnabledPlugins.ContainsKey(dllName)) || !settings.Plugins.EnabledPlugins[dllName])
            {
                settings.Plugins.EnabledPlugins[dllName] = true;
                ISettingsProvider.Instance.SaveSettings(settings);
            }
        }
        else
        {
            if (settings.Plugins.EnabledPlugins.TryGetValue(dllName, out var value) && value)
            {
                settings.Plugins.EnabledPlugins[dllName] = false;
                ISettingsProvider.Instance.SaveSettings(settings);
            }
        }

        pluginInfo.IsEnabled = enabled;

        // Disable other versions of the same plugin, and pin the version that is enabled if necessary.
        var pluginInfos = _pluginTypes.Where(p => p.ID == pluginInfo.ID).ToList();
        var highestVersion = pluginInfos.MaxBy(p => p.Version);
        foreach (var plugin in pluginInfos.Except([pluginInfo]))
            plugin.IsEnabled = false;

        return TogglePluginPin(pluginInfo, enabled && highestVersion != pluginInfo);
    }

    private LocalPluginInfo TogglePluginPin(LocalPluginInfo pluginInfo, bool pinned)
    {
        var pluginInfos = _pluginTypes.Where(p => p.ID == pluginInfo.ID).ToList();
        if (pinned && pluginInfo.IsEnabled)
        {
            pluginInfo.IsPinned = true;
            var pinnedFile = GetPinnedFile(pluginInfo.ContainingDirectory, pluginInfo.DLLs[0]);
            if (!File.Exists(pinnedFile))
                File.WriteAllText(pinnedFile, string.Empty);
        }

        foreach (var plugin in pluginInfos)
        {
            if (pinned && plugin == pluginInfo && pluginInfo.IsEnabled)
                continue;

            plugin.IsPinned = false;
            var pinnedFile = GetPinnedFile(plugin.ContainingDirectory, plugin.DLLs[0]);
            if (File.Exists(pinnedFile))
                File.Delete(pinnedFile);
        }

        return pluginInfo;
    }

    private static LocalPluginInfo TogglePluginKeep(LocalPluginInfo pluginInfo, bool kept)
    {
        if (!pluginInfo.CanUninstall || !pluginInfo.IsInstalled)
            return pluginInfo;

        var keepFile = GetKeepFile(pluginInfo.ContainingDirectory, pluginInfo.DLLs[0]);
        if (kept && !File.Exists(keepFile))
            File.WriteAllText(keepFile, string.Empty);
        else if (!kept && File.Exists(keepFile))
            File.Delete(keepFile);

        pluginInfo.IsKept = kept;
        return pluginInfo;
    }

    /// <summary>
    ///   Gets when a plugin version was installed: the last-write time of its
    ///   main DLL. A package install sets it to the install time, while a
    ///   manual copy keeps whatever time the copy gave the file.
    /// </summary>
    /// <param name="dllPath">The plugin's main DLL.</param>
    /// <returns>The install time, in UTC.</returns>
    internal static DateTime GetInstalledAt(string dllPath)
        => File.GetLastWriteTimeUtc(dllPath);

    #endregion
}
