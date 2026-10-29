using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

/// <summary>
///   Finding the plugins on disk and picking the version of each to load,
///   with the copies of versions the server ships.
/// </summary>
public partial class PluginManager
{
    #region Scan

    public void ScanForPlugins()
    {
        if (_pluginTypes.Count > 0)
            throw new InvalidOperationException("Plugins have already been registered.");

        // Before anything loads, so no plugin has its data open yet.
        RemovePluginDataMarkedForRemoval();

        // The plugins that took the last start down, read once and tried again the start after.
        var rememberedFailures = TakeRememberedFailures();

        // Add the core plugin to register it's plugin providers.
        var internalPlugins = new List<InternalPluginInfo>()
        {
            new()
            {
                ID = CorePlugin.StaticID,
                Name = "Shoko Core",
                Namespace = "shoko",
                IsMeta = false,
                DllName = Path.GetFileNameWithoutExtension(Assembly.GetCallingAssembly().Location!),
                Description = string.Empty,
                Version =  systemService.Version,
                InstalledAt = systemService.Version.ReleasedAt,
                Authors = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>() is { Company: { Length: > 0 } companyName }
                    ? companyName
                    : null,
                RepositoryUrl = null,
                HomepageUrl = null,
                Tags = [],
                IsPinned = true,
                IsEnabled = true,
                CanLoad = true,
                Priority = -1,
                CanUninstall = false,
                ContainingDirectory = null,
                DLLs = [Assembly.GetCallingAssembly().Location!],
            },
        };

        var directories = GetPluginDirectories().ToArray();
        logger.LogTrace("Scanning {Count} directories for plugins...", directories.Length);
        var settingsChanged = false;
        var settings = ISettingsProvider.Instance.GetSettings();
        foreach (var (dirPath, dlls, isSystem) in directories)
            if (LoadInternalPluginInfo(dirPath, dlls, isSystem, settings, ref settingsChanged) is { } internalPluginInfo)
                internalPlugins.Add(internalPluginInfo);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (settingsChanged)
            ISettingsProvider.Instance.SaveSettings();

        RemoveCopiesOfBundledVersions(internalPlugins);

        // Before a version is picked, so a plugin installed as a dependency loads while something needs it.
        ApplyDemand(internalPlugins, settings.Plugins.EnabledPlugins);

        var legacyReplacements = FindLegacyReplacements(internalPlugins);
        var picks = new List<(List<InternalPluginInfo> OrderedInfo, InternalPluginInfo? ToLoad, string? FailureReason, Dictionary<InternalPluginInfo, string> PassedOver)>();
        foreach (var grouping in internalPlugins.OrderBy(a => a.Priority).GroupBy(a => a.ID))
        {
            // On a tie, the copy the server ships wins over one installed beside it.
            var orderedInfo = grouping
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.Version)
                .ThenBy(a => a.CanUninstall)
                .ThenByDescending(a => a.ContainingDirectory is not null)
                .ThenBy(a => a.ContainingDirectory)
                .ThenBy(a => a.DLLs[0])
                .ToList();
            var (toLoad, failureReason, passedOver) = PickVersionToLoad(orderedInfo, rememberedFailures);
            if (toLoad is not null && legacyReplacements.TryGetValue(toLoad, out var replacement))
            {
                var reason = $"Replaced by version {replacement.Version.Version} of \"{replacement.Name}\", built for this server.";
                logger.LogInformation("Not loading legacy plugin \"{Name}\". {Reason} ({Version})", toLoad.Name, reason, toLoad.Version);
                passedOver[toLoad] = reason;
                (toLoad, failureReason) = (null, null);
            }

            picks.Add((orderedInfo, toLoad, failureReason, passedOver));
        }

        // Every load context exists before any plugin loads, so a plugin's
        // dependencies are found whichever loads first.
        CreateLoadContexts(picks.Where(pick => pick.FailureReason is null).Select(pick => pick.ToLoad).OfType<InternalPluginInfo>());

        foreach (var pick in picks)
        {
            var (orderedInfo, toLoad, failureReason, passedOver) = pick;
            foreach (var internalPluginInfo in orderedInfo)
            {
                if (internalPluginInfo != toLoad)
                {
                    var passedOverReason = passedOver.GetValueOrDefault(internalPluginInfo);
                    _pluginTypes.Add(new()
                    {
                        ID = internalPluginInfo.ID,
                        Name = internalPluginInfo.Name,
                        Description = internalPluginInfo.Description,
                        Namespace = internalPluginInfo.Namespace,
                        IsMeta = internalPluginInfo.IsMeta,
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
                        IsEnabled = false,
                        IsPinned = internalPluginInfo.IsPinned,
                        IsActive = false,
                        CanLoad = passedOverReason is null && internalPluginInfo.CanLoad,
                        CannotLoadReason = passedOverReason ?? internalPluginInfo.CannotLoadReason,
                        CanUninstall = internalPluginInfo.CanUninstall,
                        Plugin = null,
                        PluginType = null,
                        ServiceRegistrationType = null,
                        ApplicationRegistrationType = null,
                        ContainingDirectory = internalPluginInfo.ContainingDirectory,
                        DLLs = internalPluginInfo.DLLs,
                        Types = [],
                        Thumbnail = PackageImageLoader.Load(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail, ThumbnailKind, applicationPaths),
                        Icon = PackageImageLoader.Load(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Icon, IconKind, applicationPaths),
                        Dependencies = internalPluginInfo.Dependencies,
                    });
                    continue;
                }

                var mainDllPath = internalPluginInfo.DLLs[0];
                var isCore = internalPluginInfo.ID == CorePlugin.StaticID;
                Type[] types = [];
                Type? pluginType = null;
                Type? serviceRegistrationType = null;
                Type? applicationRegistrationType = null;
                if (failureReason is not null)
                    logger.LogError("Not loading plugin \"{Name}\". {Reason} ({Version})", internalPluginInfo.Name, failureReason, internalPluginInfo.Version);
                else
                {
                    try
                    {
                        var assembly = mainDllPath is null
                            ? Assembly.GetCallingAssembly()
                            : _loadContexts.TryGetValue(internalPluginInfo.ID, out var loadContext)
                                ? loadContext.LoadFromAssemblyPath(mainDllPath)
                                : Assembly.LoadFrom(mainDllPath);
                        types = assembly.GetExportedTypes();
                        pluginType = types.First(a => a.GetInterfaces().Contains(typeof(IPlugin)));
                        serviceRegistrationType = types.FirstOrDefault(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration)));
                        applicationRegistrationType = types.FirstOrDefault(a => a.GetInterfaces().Contains(typeof(IPluginApplicationRegistration)));
                    }
                    // The core failing to load is not a plugin's failure to isolate.
                    catch (Exception ex) when (!isCore)
                    {
                        logger.LogError(ex, "Plugin \"{Name}\" failed while being loaded. ({Version})", internalPluginInfo.Name, internalPluginInfo.Version);
                        failureReason = DescribeFailure("being loaded", ex);
                        types = [];
                        pluginType = null;
                        serviceRegistrationType = null;
                        applicationRegistrationType = null;
                    }
                }

                _pluginTypes.Add(new()
                {
                    ID = internalPluginInfo.ID,
                    Name = internalPluginInfo.Name,
                    Description = internalPluginInfo.Description,
                    Namespace = internalPluginInfo.Namespace,
                    IsMeta = internalPluginInfo.IsMeta,
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
                    IsEnabled = true,
                    IsPinned = internalPluginInfo.IsPinned,
                    IsActive = false,
                    CanLoad = failureReason is null && internalPluginInfo.CanLoad,
                    CannotLoadReason = failureReason ?? internalPluginInfo.CannotLoadReason,
                    FailureReason = failureReason,
                    CanUninstall = internalPluginInfo.CanUninstall,
                    Plugin = null,
                    PluginType = pluginType,
                    ServiceRegistrationType = serviceRegistrationType,
                    ApplicationRegistrationType = applicationRegistrationType,
                    ContainingDirectory = internalPluginInfo.ContainingDirectory,
                    DLLs = internalPluginInfo.DLLs,
                    Types = types,
                    Thumbnail = PackageImageLoader.Load(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail, ThumbnailKind, applicationPaths),
                    Icon = PackageImageLoader.Load(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Icon, IconKind, applicationPaths),
                    Dependencies = internalPluginInfo.Dependencies,
                });
            }
        }

        // Before the dependency graph, so what depends on a plugin failed here is refused with it.
        FailPluginsClaimingTheSameNamespace();
        FailPluginsNotFittingTheirDependencies();

        ApplyDependencyGraph(_pluginTypes, _dependencyRefusals, logger);
        _pluginsLoaded = true;
    }

    /// <summary>
    ///   Creates the load context of every plugin version about to load,
    ///   apart from the core's, which lives in the server's own.
    /// </summary>
    /// <param name="toLoad">The plugin versions about to load.</param>
    private void CreateLoadContexts(IEnumerable<InternalPluginInfo> toLoad)
    {
        foreach (var info in toLoad)
        {
            if (info.ID == CorePlugin.StaticID || info.DLLs is not [{ } mainDllPath, ..])
                continue;

            _loadContexts[info.ID] = new(
                info.ID,
                info.Name,
                mainDllPath,
                [.. info.Dependencies.Select(dependency => dependency.PluginID)],
                id => _loadContexts.GetValueOrDefault(id),
                () => _loadContexts.Values
            );
        }
    }

    #endregion

    #region Version Choice

    /// <summary>
    ///   Picks the version of a plugin to load: the first enabled one. When
    ///   that one cannot load yet, such as one built for a newer server, and
    ///   is not pinned, the newest older version that can load waits in its
    ///   place. A version that fails is never passed over; the admin decides.
    /// </summary>
    /// <param name="orderedInfo">The plugin's versions, in the order they are picked.</param>
    /// <param name="rememberedFailures">The versions that took the last start down.</param>
    /// <returns>
    ///   The version to load, if any, why it fails before loading, if it does,
    ///   and why each version passed over was.
    /// </returns>
    internal (InternalPluginInfo? ToLoad, string? FailureReason, Dictionary<InternalPluginInfo, string> PassedOver) PickVersionToLoad(
        IReadOnlyList<InternalPluginInfo> orderedInfo,
        IReadOnlyDictionary<string, string> rememberedFailures
    )
    {
        var passedOver = new Dictionary<InternalPluginInfo, string>();
        var candidates = orderedInfo.Where(info => info.IsEnabled).ToList();
        if (candidates.Count is 0)
            return (null, null, passedOver);

        // Read before the assembly loads, so a plugin built for another contract never runs. An
        // enabled plugin that cannot load fails the start, rather than going missing from it.
        string? GetFailureReason(InternalPluginInfo info)
            => info.ID == CorePlugin.StaticID
                ? null
                : !info.CanLoad
                    ? info.CannotLoadReason ?? "The plugin cannot load on this server."
                    : info.FailureReason
                        ?? rememberedFailures.GetValueOrDefault(GetRememberedFailureKey(info.ID, info.Version.Version))
                        ?? FindContractMismatch(info.DLLs, GetHostSurfaces(), HostMismatchMessage);

        var first = candidates[0];
        if (first.CanLoad || first.IsPinned || first.ID == CorePlugin.StaticID || candidates.Skip(1).FirstOrDefault(info => info.CanLoad) is not { } older)
            return (first, GetFailureReason(first), passedOver);

        var reason = first.CannotLoadReason ?? "It cannot load on this server yet.";
        logger.LogInformation(
            "Not loading version {Version} of plugin \"{Name}\" yet and loading version {OlderVersion} in its place. {Reason}",
            first.Version.Version,
            first.Name,
            older.Version.Version,
            reason
        );
        passedOver[first] = reason;
        return (older, GetFailureReason(older), passedOver);
    }

    /// <summary>
    ///   Pairs every legacy plugin with the build of the same assembly made for
    ///   this server, when one is installed and can load. A legacy plugin has
    ///   no ID of its own, so installing the new build is enough to replace it.
    /// </summary>
    /// <param name="plugins">Every plugin version found on disk.</param>
    /// <returns>The replacing build of each replaced legacy plugin.</returns>
    internal static Dictionary<InternalPluginInfo, InternalPluginInfo> FindLegacyReplacements(IReadOnlyList<InternalPluginInfo> plugins)
    {
        var builds = plugins
            .Where(info => !info.IsLegacy && info.CanLoad && info.ID != CorePlugin.StaticID)
            .OrderByDescending(info => info.Version.Version)
            .GroupBy(info => info.DllName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return plugins
            .Where(info => info.IsLegacy && builds.ContainsKey(info.DllName))
            .ToDictionary(info => info, info => builds[info.DllName]);
    }

    #endregion

    #region Bundled Copies

    /// <summary>
    ///   Finds the installed copies of a plugin version the server also ships,
    ///   which the shipped copy replaces: those neither pinned nor kept.
    /// </summary>
    /// <param name="plugins">Every plugin version found on disk.</param>
    /// <returns>The copies to remove.</returns>
    internal static IReadOnlyList<InternalPluginInfo> FindCopiesOfBundledVersions(IReadOnlyList<InternalPluginInfo> plugins)
    {
        var bundled = plugins
            .Where(info => !info.CanUninstall && !info.IsLegacy && info.ID != CorePlugin.StaticID)
            .Select(info => (info.ID, info.Version.Version))
            .ToHashSet();
        return plugins
            .Where(info => info.CanUninstall && !info.IsLegacy && !info.IsPinned && !info.IsKept && bundled.Contains((info.ID, info.Version.Version)))
            .ToList();
    }

    /// <summary>
    ///   Removes the installed copies of the plugin versions the server ships,
    ///   such as an upgrade of a bundled plugin the next server release ships
    ///   too, and leaves them out of this start. A copy that cannot be removed
    ///   now is marked for removal on the next start.
    /// </summary>
    /// <param name="plugins">Every plugin version found on disk.</param>
    private void RemoveCopiesOfBundledVersions(List<InternalPluginInfo> plugins)
    {
        foreach (var copy in FindCopiesOfBundledVersions(plugins))
        {
            plugins.Remove(copy);
            var path = copy.ContainingDirectory ?? copy.DLLs[0];
            try
            {
                if (copy.ContainingDirectory is not null)
                    Directory.Delete(copy.ContainingDirectory, recursive: true);
                else
                    File.Delete(copy.DLLs[0]);
                logger.LogInformation("Removed version {Version} of plugin \"{Name}\", as the server ships the same version. ({Path})", copy.Version.Version, copy.Name, path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not remove version {Version} of plugin \"{Name}\", which the server ships too, so it goes on the next start. ({Path})", copy.Version.Version, copy.Name, path);
                try
                {
                    File.WriteAllText(copy.ContainingDirectory is not null ? Path.Join(copy.ContainingDirectory, Remove) : Path.ChangeExtension(copy.DLLs[0], Remove), string.Empty);
                }
                catch (Exception markerEx) when (markerEx is IOException or UnauthorizedAccessException)
                {
                    logger.LogError(markerEx, "Could not mark version {Version} of plugin \"{Name}\" for removal. ({Path})", copy.Version.Version, copy.Name, path);
                }
            }
        }
    }

    #endregion
}
