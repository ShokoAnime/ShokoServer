using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.API.Swagger;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
namespace Shoko.Server.Plugin;

public partial class PluginManager(ILogger<PluginManager> logger, ISystemService systemService, IApplicationPaths applicationPaths) : IPluginManager
{
    private const string Remove = ".remove";

    private const string Pinned = ".pinned";

    private readonly List<Type> _exportedTypes = [];

    private readonly List<LocalPluginInfo> _pluginTypes = [];

    /// <summary>
    ///   The ID of the plugin owning each assembly in the default load
    ///   context, or <see langword="null"/> for one no plugin owns. Only
    ///   filled once <see cref="ScanForPlugins"/> has loaded every plugin.
    /// </summary>
    private readonly ConcurrentDictionary<Assembly, Guid?> _assemblyOwners = new();

    /// <summary>
    ///   The load state each plugin refused for its dependencies had before
    ///   it was refused, so the refusal can be taken back once they are
    ///   satisfied again.
    /// </summary>
    private readonly Dictionary<LocalPluginInfo, PluginLoadState> _dependencyRefusals = [];

    private bool _pluginsLoaded;

    private SemverVersionComparer? _semverComparer;

    private static readonly Version _invalidVersion = new(0, 0, 0, 0);

    #region Compatibility

    /// <inheritdoc/>
    public Version AbstractionVersion { get; private init; } = systemService.Version.AbstractionVersion;

    /// <inheritdoc/>
    public string RuntimeIdentifier { get; private init; } = true switch
    {
        true when OperatingSystem.IsLinux() => $"linux-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsWindows() => $"win-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsFreeBSD() => $"freebsd-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsAndroid() => $"android-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsMacCatalyst() => $"osx-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsMacOS() => $"osx-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsIOS() => $"ios-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsTvOS() => $"tvos-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsWatchOS() => $"watchos-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}",
        true when OperatingSystem.IsBrowser() => $"browser-any",
        true when OperatingSystem.IsWasi() => $"wasi-any",
        _ => "any",
    };

    public bool IsAbiAndRuntimeCompatible(Version abstractionVersion, string runtimeIdentifier) =>
        (runtimeIdentifier is "any" || runtimeIdentifier == RuntimeIdentifier) &&
        AbstractionVersion.Major == abstractionVersion.Major &&
        (_semverComparer ??= new()).Compare(AbstractionVersion, abstractionVersion) >= 0;

    #endregion

    #region Setup

    /// <summary>
    ///   Basic information about a plugin, used during initial loading before
    ///   the full <see cref="LocalPluginInfo"/> is available.
    /// </summary>
    /// <summary>
    ///   The plugin's wide image, shipped as <c>thumbnail.*</c> beside the
    ///   plugin or as <c>&lt;dll&gt;.thumbnail.*</c> when it has no directory.
    /// </summary>
    private const string ThumbnailKind = "thumbnail";

    /// <summary>
    ///   The plugin's square mark, named the same way as
    ///   <see cref="ThumbnailKind"/>.
    /// </summary>
    private const string IconKind = "icon";

    /// <summary>
    ///   Whether a plugin can load, and why not when it cannot.
    /// </summary>
    /// <param name="CanLoad">Whether the plugin can load.</param>
    /// <param name="CannotLoadReason">Why the plugin cannot load, when known.</param>
    internal readonly record struct PluginLoadState(bool CanLoad, string? CannotLoadReason);

    private sealed class InternalPluginInfo
    {
        /// <summary>
        ///   The unique identifier for the plugin.
        /// </summary>
        public required Guid ID { get; init; }

        /// <summary>
        ///   The name of the plugin.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        ///   The description of the plugin.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        ///   The version of the plugin.
        /// </summary>
        public required VersionInformation Version { get; init; }

        /// <summary>
        ///   The author(s) of the plugin.
        /// </summary>
        public required string? Authors { get; init; }

        /// <summary>
        ///   The repository URL for the package and plugin releases contained
        ///   within it, if provided.
        /// </summary>
        public required string? RepositoryUrl { get; init; }

        /// <summary>
        ///   The home-page URL for the package and plugin releases contained
        ///   within it, if provided.
        /// </summary>
        public required string? HomepageUrl { get; init; }

        /// <summary>
        ///   The search tags for the plugin. A maximum of 10 tags will be
        ///   loaded if provided.
        /// </summary>
        public required IReadOnlyList<string> Tags { get; init; }

        /// <summary>
        ///   The priority of the plugin for loading order. Lower values load first.
        /// </summary>
        /// <remarks>
        ///   Will be <c>-1</c> if the plugin is not yet loaded.
        /// </remarks>
        public required int Priority { get; init; }

        /// <summary>
        /// When the plugin was installed locally, or <c>null</c> if the plugin is
        /// not installed locally.
        /// </summary>
        public required DateTime InstalledAt { get; init; }

        /// <summary>
        ///   Indicates the plugin is pinned and should not be automatically updated
        ///   or reordered based on version.
        /// </summary>
        public required bool IsPinned { get; init; }

        /// <summary>
        ///   Indicates the plugin is enabled and should be loaded.
        /// </summary>
        public required bool IsEnabled { get; init; }

        /// <summary>
        ///   Indicates the plugin can be loaded by the current runtime. Missing
        ///   assemblies or incompatible ABI versions will prevent loading.
        /// </summary>
        public required bool CanLoad { get; init; }

        /// <summary>
        ///   Why the plugin cannot be loaded, when it is known.
        /// </summary>
        public string? CannotLoadReason { get; init; }

        /// <summary>
        ///   Indicates if the plugin can be uninstalled by the user. System plugins
        ///   cannot be uninstalled.
        /// </summary>
        public required bool CanUninstall { get; init; }

        /// <summary>
        ///   The name of the DLL file containing the plugin implementation.
        /// </summary>
        public required string DllName { get; init; }

        /// <summary>
        ///   The directory containing the plugin DLLs, if the plugin is not placed
        ///   in the root of the plugins directory.
        /// </summary>
        public required string? ContainingDirectory { get; init; }

        /// <summary>
        ///   All DLLs for the plugin. The first path will always be the main DLL
        ///   which contains the plugin implementation.
        /// </summary>
        public required string[] DLLs { get; init; }

        /// <summary>
        ///   The raw thumbnail image byte array for the plugin, if available.
        /// </summary>
        public byte[]? Thumbnail { get; set; }

        public byte[]? Icon { get; set; }

        /// <summary>
        ///   The dependencies of this plugin version.
        /// </summary>
        public IReadOnlyList<PluginDependency> Dependencies { get; init; } = [];
    }

    private static string GetPinnedFile(string? directory, string dll)
        => string.IsNullOrEmpty(directory)
            ? Path.ChangeExtension(dll, Pinned)
            : Path.Join(directory, Pinned);

    private static string GetRemovalFile(string? directory, string dll)
        => string.IsNullOrEmpty(directory)
            ? Path.ChangeExtension(dll, Remove)
            : Path.Join(directory, Remove);

    private class IsolatedLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver? _resolver;

        public IsolatedLoadContext(string? selfResolvingPluginPath = null) : base(isCollectible: true)
        {
            if (selfResolvingPluginPath is not null)
                _resolver = new AssemblyDependencyResolver(selfResolvingPluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (_resolver is null)
                return base.Load(assemblyName);
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            if (_resolver is null)
                return base.LoadUnmanagedDll(unmanagedDllName);
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is not null ? LoadUnmanagedDllFromPath(libraryPath) : nint.Zero;
        }
    }

    public void ScanForPlugins()
    {
        if (_pluginTypes.Count > 0)
            throw new InvalidOperationException("Plugins have already been registered.");

        // Before anything loads, so no plugin has its data open yet.
        RemovePluginDataMarkedForRemoval();

        // Add the core plugin to register it's plugin providers.
        var internalPlugins = new List<InternalPluginInfo>()
        {
            new()
            {
                ID = CorePlugin.StaticID,
                Name = "Shoko Core",
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

        foreach (var grouping in internalPlugins.OrderBy(a => a.Priority).GroupBy(a => a.ID))
        {
            var orderedInfo = grouping
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.Version)
                .ThenByDescending(a => a.ContainingDirectory is not null)
                .ThenBy(a => a.ContainingDirectory)
                .ThenBy(a => a.DLLs[0])
                .ToList();
            var enabled = true;
            foreach (var internalPluginInfo in orderedInfo)
            {
                if (!internalPluginInfo.IsEnabled || !enabled)
                {
                    _pluginTypes.Add(new()
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
                        IsEnabled = false,
                        IsPinned = internalPluginInfo.IsPinned,
                        IsActive = false,
                        CanLoad = internalPluginInfo.CanLoad,
                        CannotLoadReason = internalPluginInfo.CannotLoadReason,
                        CanUninstall = internalPluginInfo.CanUninstall,
                        Plugin = null,
                        PluginType = null,
                        ServiceRegistrationType = null,
                        ApplicationRegistrationType = null,
                        ContainingDirectory = internalPluginInfo.ContainingDirectory,
                        DLLs = internalPluginInfo.DLLs,
                        Types = [],
                        Thumbnail = LoadPluginImageInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail, ThumbnailKind),
                        Icon = LoadPluginImageInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Icon, IconKind),
                        Dependencies = internalPluginInfo.Dependencies,
                    });
                    continue;
                }

                enabled = false;
                var mainDllPath = internalPluginInfo.DLLs[0];
                var assembly = mainDllPath is null ? Assembly.GetCallingAssembly() : Assembly.LoadFrom(mainDllPath);
                var types = assembly.GetExportedTypes();
                _pluginTypes.Add(new()
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
                    IsEnabled = true,
                    IsPinned = internalPluginInfo.IsPinned,
                    IsActive = false,
                    CanLoad = internalPluginInfo.CanLoad,
                    CannotLoadReason = internalPluginInfo.CannotLoadReason,
                    CanUninstall = internalPluginInfo.CanUninstall,
                    Plugin = null,
                    PluginType = types.First(a => a.GetInterfaces().Contains(typeof(IPlugin))),
                    ServiceRegistrationType = types.FirstOrDefault(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration))),
                    ApplicationRegistrationType = types.FirstOrDefault(a => a.GetInterfaces().Contains(typeof(IPluginApplicationRegistration))),
                    ContainingDirectory = internalPluginInfo.ContainingDirectory,
                    DLLs = internalPluginInfo.DLLs,
                    Types = types,
                    Thumbnail = LoadPluginImageInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail, ThumbnailKind),
                    Icon = LoadPluginImageInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Icon, IconKind),
                    Dependencies = internalPluginInfo.Dependencies,
                });
            }
        }

        ApplyDependencyGraph(_pluginTypes, _dependencyRefusals, logger);
        _pluginsLoaded = true;
    }

    /// <summary>
    ///   Orders the plugins so every dependency loads before its dependents,
    ///   and refuses every enabled plugin whose required dependencies are not
    ///   satisfied.
    /// </summary>
    /// <param name="plugins">Every registered plugin version. Reordered in place.</param>
    /// <param name="logger">The logger to write the refusals to.</param>
    internal static void ApplyDependencyGraph(List<LocalPluginInfo> plugins, ILogger logger)
        => ApplyDependencyGraph(plugins, [], logger);

    /// <summary>
    ///   Orders the plugins so every dependency loads before its dependents,
    ///   and refuses every enabled plugin whose required dependencies are not
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

    public void RegisterPlugins(IServiceCollection serviceCollection)
    {
        // Register the plugins in order of priority & then register their services.
        var registrationPlugins = _pluginTypes
            .Where(a => a is { CanLoad: true, ServiceRegistrationType: not null })
            .ToList();
        if (registrationPlugins.Count > 0)
            logger.LogTrace("Registering services for {Count} plugins.", registrationPlugins.Count);

        foreach (var pluginInfo in registrationPlugins)
        {
            logger.LogTrace("Registering plugin services. ({DllName}, {Version})", Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]), pluginInfo.Version);
            pluginInfo.ServiceRegistrationType!
                .GetMethod(nameof(IPluginServiceRegistration.RegisterServices), BindingFlags.Public | BindingFlags.Static, [typeof(IServiceCollection), typeof(IApplicationPaths)])!
                .Invoke(null, [serviceCollection, applicationPaths]);
        }

        // The databases plugins asked for become contexts on the provider the core picks.
        PluginDatabaseRegistrar.AddPluginDatabases(serviceCollection);

        // Scan every loaded plugin assembly for IQueueJob implementations.
        // Plugins don't need to call AddQueueJobsFromAssembly themselves.
        foreach (var pluginInfo in _pluginTypes.Where(a => a is { CanLoad: true, PluginType: not null }))
        {
            var assembly = pluginInfo.PluginType!.Assembly;
            if (assembly == typeof(PluginManager).Assembly)
                continue; //Skip the current assembly, as this is registered implicitly.

            logger.LogTrace("Scanning plugin assembly for queue jobs. ({DllName})", Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]));
            serviceCollection.AddQueueJobsFromAssembly(pluginInfo.PluginType!.Assembly);
        }

        // One job type per metadata provider, so each can be paused and limited alone; the closed
        // types are only known once the providers are found.
        var providerTypes = _pluginTypes
            .Where(pluginInfo => pluginInfo.CanLoad)
            .SelectMany(pluginInfo => pluginInfo.Types)
            .ToList();
        foreach (var providerType in providerTypes)
            foreach (var overlong in MetadataProviderJobs.GetOverlongJobTypes(providerType))
                logger.LogWarning(
                    "Not registering {JobType} for {Provider}: its stored name is longer than the queue keeps, so its jobs cannot be queued.",
                    JobTypeNames.Short(overlong), providerType.FullName
                );

        // One job type per image contributor too, so each gets a pool of its own.
        foreach (var contributorType in providerTypes)
            if (MetadataImageContributorJobs.GetOverlongJobType(contributorType) is { } overlong)
                logger.LogWarning(
                    "Not registering {JobType} for {Contributor}: its stored name is longer than the queue keeps, so its jobs cannot be queued.",
                    JobTypeNames.Short(overlong), contributorType.FullName
                );

        var providerJobTypes = providerTypes
            .SelectMany(MetadataProviderJobs.GetJobTypes)
            .Concat(providerTypes.Select(MetadataImageContributorJobs.GetJobType).OfType<Type>())
            .ToList();
        if (providerJobTypes.Count > 0)
        {
            logger.LogTrace("Registering {Count} metadata provider and image contributor job types.", providerJobTypes.Count);
            serviceCollection.AddQueueJobTypes(providerJobTypes);
        }

        // Transient, so the jobs running them resolve them fresh per execution.
        foreach (var actionType in _pluginTypes
                     .Where(pluginInfo => pluginInfo.CanLoad)
                     .SelectMany(pluginInfo => pluginInfo.Types)
                     .Where(type => type is { IsClass: true, IsAbstract: false } &&
                         (typeof(IExecutableAction).IsAssignableFrom(type) || typeof(IScheduledAction).IsAssignableFrom(type))))
        {
            serviceCollection.TryAddTransient(actionType);
        }
    }

    /// <summary>
    /// Hands every loaded plugin the container, so it can take the services it needs.
    /// </summary>
    /// <remarks>
    /// Not part of <see cref="InitPlugins"/>: a plugin's services cannot be taken back out of a
    /// built container, so a failing plugin cannot be quietly dropped. The caller records the
    /// failure and starts the web host anyway, so the server can say which plugin stopped it.
    /// Registration of metadata sources and entity types closes afterwards either way.
    /// </remarks>
    /// <exception cref="AggregateException">One or more plugins threw while setting themselves up.</exception>
    public void SetupPlugins()
    {
        try
        {
            RunForEveryActivePlugin(plugin => plugin.Setup(ISystemService.StaticServices), "setting itself up");
        }
        finally
        {
            CloseMetadataRegistration(logger);
        }
    }

    /// <summary>
    /// Hands every loaded plugin the given container, so it can take the services it needs, then
    /// closes registration of metadata sources and entity types.
    /// </summary>
    /// <param name="services">The container to hand the plugins.</param>
    /// <exception cref="AggregateException">One or more plugins threw while setting themselves up.</exception>
    internal void SetupPlugins(IServiceProvider services)
    {
        try
        {
            RunForEveryActivePlugin(plugin => plugin.Setup(services), "setting itself up");
        }
        finally
        {
            CloseMetadataRegistration(logger);
        }
    }

    /// <summary>
    /// Starts the plugins in two steps: sets every plugin up, then makes every plugin ready, the
    /// second only when the first succeeded. The plugin databases are not migrated here but in the
    /// late start, after the core's database, so neither step can use them.
    /// </summary>
    /// <param name="services">The container, to hand the plugins.</param>
    /// <param name="reportProgress">Told what is happening before each step.</param>
    /// <exception cref="AggregateException">One or more plugins threw while setting themselves up or getting ready.</exception>
    internal void StartPlugins(IServiceProvider services, Action<string> reportProgress)
    {
        reportProgress("Setting up plugins.");
        SetupPlugins(services);

        reportProgress("Getting plugins ready.");
        ReadyPlugins();
    }

    /// <summary>
    /// Closes registration of every <see cref="MetadataSource"/> and <see cref="MetadataEntityType"/>,
    /// once no plugin can still be setting itself up, and logs what was registered. Safe to call
    /// more than once; only the call that closes it logs.
    /// </summary>
    /// <param name="logger">The logger to write the registered sources and entity types to.</param>
    internal static void CloseMetadataRegistration(ILogger logger)
    {
        if (MetadataSource.IsFrozen && MetadataEntityType.IsFrozen)
            return;

        MetadataSource.Freeze();
        MetadataEntityType.Freeze();
        MetadataRouteParameterFilter.Capture();

        var sources = MetadataSource.All;
        var entityTypes = MetadataEntityType.All;
        logger.LogInformation(
            "Closed registration of metadata sources and entity types after plugin setup, with {SourceCount} sources ({Sources}) and {EntityTypeCount} entity types ({EntityTypes}).",
            sources.Count,
            string.Join(", ", sources.Select(source => source.Value)),
            entityTypes.Count,
            string.Join(", ", entityTypes.Select(entityType => entityType.Value))
        );
        foreach (var source in sources)
            logger.LogDebug("Registered metadata source {Value} ({Name}): {Description}", source.Value, source.Name, source.Description ?? "no description");
        foreach (var entityType in entityTypes)
            logger.LogDebug("Registered metadata entity type {Value} ({Name}): {Description}", entityType.Value, entityType.Name, entityType.Description ?? "no description");
    }

    /// <summary>
    /// Tells every loaded plugin that all plugins have been set up.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SetupPlugins()"/> because setup runs in load order, so a plugin that
    /// collects what other plugins contribute cannot see them all from its own <c>Setup</c>.
    /// </remarks>
    /// <exception cref="AggregateException">One or more plugins threw while getting ready.</exception>
    public void ReadyPlugins()
        => RunForEveryActivePlugin(plugin => plugin.Ready(), "getting ready");

    /// <summary>
    /// Runs one start-up step on every active plugin, and only then fails if any of them threw.
    /// </summary>
    /// <remarks>
    /// Stopping at the first failure would leave every later plugin without the step, and its
    /// hosted services and middleware would still start once the web host does, assuming state it
    /// never got. Running them all means only the plugins that actually failed are left unset, and
    /// the error names every one of them rather than the first.
    /// </remarks>
    /// <param name="step">The step to run on each plugin.</param>
    /// <param name="doing">What the step is, for the log and the error.</param>
    /// <exception cref="AggregateException">One or more plugins threw.</exception>
    private void RunForEveryActivePlugin(Action<IPlugin> step, string doing)
    {
        var failures = new List<(string Name, Exception Error)>();
        foreach (var localPluginInfo in _pluginTypes.ToArray())
        {
            if (!localPluginInfo.IsActive)
                continue;

            try
            {
                step(localPluginInfo.Plugin);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin \"{Name}\" threw while {Doing}. ({Version})", localPluginInfo.Name, doing, localPluginInfo.Version);
                failures.Add((localPluginInfo.Name, ex));
            }
        }

        if (failures.Count is 0)
            return;

        var names = string.Join(", ", failures.Select(failure => $"\"{failure.Name}\""));
        var message = failures.Count is 1
            ? $"Plugin {names} threw while {doing}."
            : $"Plugins {names} threw while {doing}.";
        throw new AggregateException(message, failures.Select(failure => failure.Error));
    }

    public void InitPlugins()
    {
        if (_exportedTypes.Count > 0)
            throw new InvalidOperationException("Plugins have already been initialized.");

        logger.LogInformation("Initializing {Count} plugins. ({Disabled} disabled)", _pluginTypes.Count(a => a.IsEnabled), _pluginTypes.Count(a => !a.IsEnabled));
        foreach (var localPluginInfo in _pluginTypes.ToArray())
        {
            var dllName = Path.GetFileNameWithoutExtension(localPluginInfo.DLLs[0]);
            if (!localPluginInfo.IsEnabled)
            {
                if (localPluginInfo.CanLoad)
                    logger.LogInformation("Skipping disabled plugin \"{Name}\". ({DllName}, {Version})", localPluginInfo.Name, dllName, localPluginInfo.Version);
                continue;
            }

            if (!localPluginInfo.CanLoad)
            {
                logger.LogWarning("Skipping enabled plugin \"{Name}\" because it cannot be loaded. ({DllName}, {Version})", localPluginInfo.Name, dllName, localPluginInfo.Version);
                continue;
            }

            var pluginType = localPluginInfo.PluginType!;
            var pluginInstance = (IPlugin)ActivatorUtilities.CreateInstance(ISystemService.StaticServices, pluginType);
            _pluginTypes[localPluginInfo.LoadOrder] = new()
            {
                ID = pluginInstance.ID,
                Name = pluginInstance.Name,
                Description = pluginInstance.Description?.CleanDescription() ?? string.Empty,
                Version = localPluginInfo.Version,
                Authors = localPluginInfo.Authors,
                RepositoryUrl = localPluginInfo.RepositoryUrl,
                HomepageUrl = localPluginInfo.HomepageUrl,
                Tags = localPluginInfo.Tags,
                LoadOrder = localPluginInfo.LoadOrder,
                InstalledAt = localPluginInfo.InstalledAt,
                IsEnabled = true,
                IsPinned = localPluginInfo.IsPinned,
                IsActive = true,
                CanLoad = localPluginInfo.CanLoad,
                CannotLoadReason = localPluginInfo.CannotLoadReason,
                CanUninstall = localPluginInfo.CanUninstall,
                Plugin = pluginInstance,
                PluginType = pluginType,
                ServiceRegistrationType = localPluginInfo.ServiceRegistrationType,
                ApplicationRegistrationType = localPluginInfo.ApplicationRegistrationType,
                ContainingDirectory = localPluginInfo.ContainingDirectory,
                DLLs = localPluginInfo.DLLs,
                Types = localPluginInfo.Types,
                Thumbnail = localPluginInfo.Thumbnail,
                Icon = localPluginInfo.Icon,
                Dependencies = localPluginInfo.Dependencies,
            };
            _exportedTypes.AddRange(localPluginInfo.Types);

            logger.LogInformation("Initialized plugin \"{Name}\". ({DllName}, {Version})", pluginInstance.Name, dllName, localPluginInfo.Version);
        }

        // Adding parts is a start-up step the host owns, so it is not on the service interfaces a
        // plugin can reach. Each service is resolved through its interface and only fed its parts
        // when it is the implementation that takes them.
        var services = ISystemService.StaticServices;

        if (services.GetRequiredService<IConfigurationService>() is ConfigurationService configurationService)
            configurationService.AddParts(GetTypes<IConfiguration>());

        if (services.GetRequiredService<IMetadataService>() is MetadataService metadataService)
            metadataService.AddParts(GetExports<IResourceResolver>(), GetExports<IMetadataResolver>());

        services.GetRequiredService<MetadataProviderManager>().AddParts(GetExports<IMetadataProvider>());

        services.GetRequiredService<MetadataImageContributorManager>().AddParts(GetExports<IMetadataImageContributor>());

        if (services.GetRequiredService<IVideoService>() is VideoService videoService)
            videoService.AddParts(GetExports<IManagedFolderIgnoreRule>());

        // Used to store the updated priorities for the providers in the settings file.
        if (services.GetRequiredService<IVideoReleaseService>() is VideoReleaseService videoReleaseService)
            videoReleaseService.AddParts(GetExports<IReleaseInfoProvider>());

        if (services.GetRequiredService<IVideoHashingService>() is VideoHashingService videoHashingService)
            videoHashingService.AddParts(GetExports<IHashProvider>());

        if (services.GetRequiredService<IAiringScheduleService>() is AiringScheduleService airingScheduleService)
            airingScheduleService.AddParts(GetExports<IAiringScheduleProvider>());

        if (services.GetRequiredService<IVideoRelocationService>() is VideoRelocationService relocationService)
            relocationService.AddParts(GetExports<IRelocationProvider>());

        var actionService = services.GetRequiredService<ActionService>();
        actionService.AddParts(GetTypes<IExecutableAction>()
            .Select(type => (GetPluginInfo(type.Assembly)!.ID, type)));

        services.GetRequiredService<ScheduledActionRegistry>().AddParts(GetTypes<IScheduledAction>()
            .Select(type => (GetPluginInfo(type.Assembly)!.ID, type)));

        if (services.GetRequiredService<IVideoStreamPipelineService>() is VideoStreamPipelineService videoStreamPipelineService)
        {
            videoStreamPipelineService.AddTransformParts(GetExports<IVideoStreamTransform>());
            videoStreamPipelineService.AddObserverParts(GetExports<IPlaybackObserver>());
        }
    }

    private IEnumerable<(string?, string[], bool)> GetPluginDirectories()
    {
        // Load plugins from the system directory.
        var systemPluginDir = Path.Join(applicationPaths.ApplicationPath, "plugins");
        if (Directory.Exists(systemPluginDir))
        {
            foreach (var filePath in Directory.GetFiles(systemPluginDir, "*.dll", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.ChangeExtension(filePath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin DLL file marked for removal: {Path}", filePath);
                    try
                    {
                        File.Delete(filePath);
                        File.Delete(removeFile);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin DLL file marked for removal: {Path}", filePath);
                    }
                    continue;
                }

                yield return (null, [filePath], true);
            }

            foreach (var directoryPath in Directory.GetDirectories(systemPluginDir, "*", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.Join(directoryPath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin directory marked for removal: {Path}", directoryPath);
                    try
                    {
                        Directory.Delete(directoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin directory marked for removal: {Path}", directoryPath);
                    }
                    continue;
                }

                yield return (directoryPath, Directory.GetFiles(directoryPath, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }), true);
            }
        }

        // Load plugins from the user config directory.
        var userPluginDir = applicationPaths.PluginsPath;
        if (Directory.Exists(userPluginDir))
        {
            foreach (var filePath in Directory.GetFiles(userPluginDir, "*.dll", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.ChangeExtension(filePath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin DLL file marked for removal: {Path}", filePath);
                    try
                    {
                        File.Delete(filePath);
                        File.Delete(removeFile);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin DLL file marked for removal: {Path}", filePath);
                    }
                    continue;
                }

                yield return (null, [filePath], false);
            }

            foreach (var directoryPath in Directory.GetDirectories(userPluginDir, "*", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                // Skip repositories metadata directory.
                if (Path.GetFileName(directoryPath) is PluginPackageManager.Repositories)
                    continue;

                var removeFile = Path.Join(directoryPath, Remove);
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin directory marked for removal: {Path}", directoryPath);
                    try
                    {
                        Directory.Delete(directoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to remove plugin directory marked for removal: {Path}", directoryPath);
                    }
                    continue;
                }

                yield return (directoryPath, Directory.GetFiles(directoryPath, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }), false);
            }
        }
    }

    private InternalPluginInfo? LoadInternalPluginInfo(string? containingDirectory, string[] dlls)
    {
        var settingsChanged = false;
        var settings = ISettingsProvider.Instance.GetSettings();
        var internalPluginInfo = LoadInternalPluginInfo(containingDirectory, dlls, false, settings, ref settingsChanged);
        if (settingsChanged)
            ISettingsProvider.Instance.SaveSettings();

        if (internalPluginInfo is not null)
            logger.LogInformation("Loaded inactive plugin \"{Name}\". ({DllName}, {Version})", internalPluginInfo.Name, Path.GetFileNameWithoutExtension(internalPluginInfo.DLLs[0]), internalPluginInfo.Version);
        return internalPluginInfo;
    }

    private const string LegacyNamespaceMessage = "This plugin uses the deprecated Shoko.Plugin.Abstractions namespace and is incompatible with this version of Shoko Server. Please update the plugin to use Shoko.Abstractions.";

    private const string AbiTooNewMessage = "The plugin failed to load because it references a newer version of Shoko.Abstractions than what this server supports.";

    private const string MissingDependenciesMessage = "The plugin failed to load due to missing dependencies.";

    private const string InvalidDependenciesMessage = "The plugin failed to load because its embedded list of plugin dependencies is invalid.";

    /// <summary>
    ///   Picks the DLL of a plugin folder that resolves its own dependencies:
    ///   the first with a <c>.deps.json</c> that holds an <see cref="IPlugin"/>,
    ///   since a plugin's own libraries may ship a <c>.deps.json</c> too.
    /// </summary>
    /// <param name="dlls">The folder's DLLs.</param>
    /// <returns>
    ///   The DLL, the first with a <c>.deps.json</c> when none of them holds
    ///   an <see cref="IPlugin"/>, or <c>null</c> when none has a
    ///   <c>.deps.json</c>.
    /// </returns>
    private string? FindSelfResolvingPlugin(string[] dlls)
    {
        var candidates = dlls.Where(dll => Path.Exists(Path.ChangeExtension(dll, ".deps.json"))).ToList();
        if (candidates.Count <= 1)
            return candidates.FirstOrDefault();

        foreach (var candidate in candidates)
        {
            if (HoldsPlugin(candidate))
                return candidate;

            logger.LogDebug("Passing over a DLL with a .deps.json because it holds no IPlugin; {DllPath}", candidate);
        }

        return candidates[0];
    }

    /// <summary>
    ///   Whether a DLL defines a type implementing <see cref="IPlugin"/>, or
    ///   the legacy namespace's <c>IPlugin</c>, read in a load context of its
    ///   own that is unloaded again.
    /// </summary>
    /// <param name="dllPath">The DLL, with its <c>.deps.json</c> beside it.</param>
    /// <returns><c>true</c> when one of the types it could load implements it.</returns>
    private static bool HoldsPlugin(string dllPath)
    {
        var alc = new IsolatedLoadContext(dllPath);
        try
        {
            Type?[] types;
            try
            {
                types = alc.LoadFromAssemblyPath(dllPath).GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            return types.Any(type =>
            {
                try
                {
                    return type is not null && type.GetInterfaces().Any(interfaceType => interfaceType == typeof(IPlugin) || interfaceType.FullName is "Shoko.Plugin.Abstractions.IPlugin");
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            alc.Unload();
        }
    }

    private InternalPluginInfo? LoadInternalPluginInfo(string? dirPath, string[] dlls, bool isSystem, IServerSettings settings, ref bool settingsChanged)
    {
        var selfResolvingPluginPath = dirPath is not null ? FindSelfResolvingPlugin(dlls) : dlls.FirstOrDefault(dll => Path.Exists(Path.ChangeExtension(dll, ".deps.json")));
        var dllsToLoad = dirPath is not null && selfResolvingPluginPath is not null ? [selfResolvingPluginPath] : dlls;
        var alc = new IsolatedLoadContext(selfResolvingPluginPath);
        try
        {
            foreach (var dllPath in dllsToLoad)
            {
                var name = Path.GetFileNameWithoutExtension(dllPath);
                try
                {
                    var assembly = alc.LoadFromAssemblyPath(dllPath);
                    var assemblyName = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(assemblyName) || !string.Equals(assemblyName, name, StringComparison.Ordinal))
                    {
                        logger.LogInformation("Skipping DLL because the loaded assembly does not have the same name as the file; {DllPath}", dllPath);
                        continue;
                    }

                    var version = ReadVersionInformationFromAssembly(assembly, out var isLegacyNamespace, out var metadataAttributeDict);
                    if (version is null)
                    {
                        // A DLL that references the abstractions is meant to be a plugin, so say why it is left out.
                        if (assembly.GetReferencedAssemblies().Any(reference => reference.Name is "Shoko.Abstractions" or "Shoko.Plugin.Abstractions"))
                            logger.LogWarning("Skipping plugin DLL because its assembly version is 0.0.0 or missing; {DllPath}", dllPath);
                        continue;
                    }

                    var authors = assembly.GetCustomAttribute<AssemblyCompanyAttribute>() is { Company: { Length: > 0 } companyName }
                        ? companyName
                        : null;
                    var repositoryUrl = metadataAttributeDict.ContainsKey(RepositoryUrl) && metadataAttributeDict[RepositoryUrl] is { Length: > 0 } ? metadataAttributeDict[RepositoryUrl] : null;
                    var homepageUrl = metadataAttributeDict.ContainsKey(PackageProjectUrl) && metadataAttributeDict[PackageProjectUrl] is { Length: > 0 } ? metadataAttributeDict[PackageProjectUrl] : null;
                    var tags = metadataAttributeDict.ContainsKey(PackageTags) &&
                        metadataAttributeDict[PackageTags]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } packageTags
                        ? packageTags.Select(tag => tag.ToLowerInvariant()).Distinct().ToArray()
                        : [];

                    // TryAdd, because if it made it this far, then it's missing or true.
                    if (settings.Plugins.EnabledPlugins.TryAdd(name, true))
                        settingsChanged = true;

                    var createdAt = File.GetCreationTimeUtc(dllPath);
                    if (isLegacyNamespace)
                    {
                        logger.LogWarning("Found plugin using deprecated Shoko.Plugin.Abstractions namespace. This plugin is incompatible and needs to be updated. ({DllName}, {Version})", name, version);

                        // Legacy plugins won't have embedded metadata, so
                        // generate a deterministic stub ID instead.
                        return new()
                        {
                            // Create an unique ID for this specific version that failed to load.
                            ID = UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = name,
                            Description = isLegacyNamespace
                                ? LegacyNamespaceMessage
                                : MissingDependenciesMessage,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = createdAt,
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsEnabled = false,
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = LegacyNamespaceMessage,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    // Read embedded identity if possible for the paths below.
                    // Legacy plugins (checked above) won't have these.
                    var embeddedId = metadataAttributeDict.TryGetValue(PackageID, out var pid) && Guid.TryParse(pid, out var parsedId)
                        ? parsedId
                        : (Guid?)null;
                    var embeddedName = metadataAttributeDict.TryGetValue(PackageName, out var pn) ? pn : null;
                    var embeddedDescription = metadataAttributeDict.TryGetValue(PackageOverview, out var pd) ? pd : null;
                    if (embeddedId is null)
                    {
                        logger.LogWarning(
                            "Plugin does not have embedded identity metadata. ({DllName}, {Version}) " +
                            "Install Shoko.BuildTools (`dotnet tool install --global Shoko.BuildTools`) " +
                            "and rebuild with `shoko-build`, or add the `Shoko.BuildTools.Targets` " +
                            "NuGet package to your project for automatic metadata injection.",
                            name, version);
                    }

                    if (version.AbstractionVersion > AbstractionVersion)
                    {
                        logger.LogInformation("Skipping DLL because the loaded assembly references a newer version of Shoko.Abstractions than what this server supports; {DllPath}", dllPath);
                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription is not null
                                ? $"{AbiTooNewMessage}\n\n{embeddedDescription}"
                                : AbiTooNewMessage,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = createdAt,
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsEnabled = false,
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = AbiTooNewMessage,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    // A dependency we cannot read is one we cannot enforce, so
                    // the plugin does not load at all.
                    var embeddedDependencies = PluginDependencyList.Parse(
                        metadataAttributeDict.TryGetValue(PackageDependencies, out var depsRaw) ? depsRaw : null,
                        out var dependencyErrors);
                    dependencyErrors.AddRange(PluginDependencyList.Validate(embeddedDependencies));
                    if (dependencyErrors.Count > 0)
                    {
                        logger.LogError(
                            "Skipping DLL because its embedded plugin dependencies are invalid: {Errors}; {DllPath}",
                            string.Join(" ", dependencyErrors),
                            dllPath);
                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription is not null
                                ? $"{InvalidDependenciesMessage}\n\n{embeddedDescription}"
                                : InvalidDependenciesMessage,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = createdAt,
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsEnabled = false,
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = InvalidDependenciesMessage,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    Type[] pluginTypes;
                    try
                    {
                        pluginTypes = assembly.GetExportedTypes();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to get exported types from DLL. Ensure all dependencies are available; {DllPath}", dllPath);

                        return new()
                        {
                            // Create an unique ID for this specific version if it failed to load.
                            ID = embeddedId ?? UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = embeddedName ?? name,
                            Description = embeddedDescription is not null
                                ? $"{MissingDependenciesMessage}\n\n{embeddedDescription}"
                                : MissingDependenciesMessage,
                            Version = version,
                            Authors = authors,
                            RepositoryUrl = repositoryUrl,
                            HomepageUrl = homepageUrl,
                            Tags = tags,
                            InstalledAt = createdAt,
                            IsPinned = string.IsNullOrEmpty(dirPath)
                                ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                                : File.Exists(Path.Join(dirPath, Pinned)),
                            IsEnabled = false,
                            ContainingDirectory = dirPath,
                            Priority = settings.Plugins.Priority.Contains(name) ? settings.Plugins.Priority.IndexOf(name) : int.MaxValue,
                            CanLoad = false,
                            CannotLoadReason = MissingDependenciesMessage,
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                            Icon = null,
                        };
                    }

                    var pluginImpl = pluginTypes
                        .Where(a => a.GetInterfaces().Contains(typeof(IPlugin)))
                        .ToList();
                    if (pluginImpl.Count == 0)
                        continue;

                    if (pluginImpl.Count > 1)
                    {
                        logger.LogWarning(
                            "Multiple implementations of IPlugin found in {DllName}. Using the first implementation: {ImplName}",
                            name,
                            pluginImpl[0].Name
                        );
                    }

                    var registrationImpl = pluginTypes
                        .Where(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration)))
                        .ToList();
                    if (registrationImpl.Count > 1)
                    {
                        logger.LogWarning(
                            "Multiple IPluginServiceRegistrations found in {DllName}. Using the first implementation: {ImplName}",
                            name,
                            registrationImpl[0].Name
                        );
                    }

                    if (registrationImpl.Count > 0)
                        logger.LogInformation("Found plugin with services. ({DllName}, {Version})", name, version);
                    else
                        logger.LogInformation("Found plugin. ({DllName}, {Version})", name, version);

                    if (!settings.Plugins.Priority.Contains(name))
                    {
                        settings.Plugins.Priority.Add(name);
                        settingsChanged = true;
                    }

                    var instance = (IPlugin)Activator.CreateInstance(pluginImpl[0])!;
                    if (embeddedId.HasValue)
                    {
                        if (embeddedId.Value == CorePlugin.StaticID)
                        {
                            logger.LogWarning("Skipping {DllName} because it has the same ID as the core plugin.", dllPath);
                            continue;
                        }
                        if (embeddedId.Value != instance.ID)
                        {
                            logger.LogWarning("Skipping {DllName} because it has a different ID from the embedded metadata.", dllPath);
                            continue;
                        }
                    }
                    else if (instance.ID == CorePlugin.StaticID)
                    {
                        logger.LogWarning("Skipping {DllName} because it has the same ID as the core plugin.", dllPath);
                        continue;
                    }
                    var thumbnailImage = ReadEmbeddedImage(assembly, assemblyName, instance.EmbeddedThumbnailResourceName, ThumbnailKind, dllPath);
                    var iconImage = ReadEmbeddedImage(assembly, assemblyName, instance.EmbeddedIconResourceName, IconKind, dllPath);

                    return new()
                    {
                        ID = embeddedId ?? instance.ID,
                        DllName = name,
                        Name = embeddedName ?? instance.Name,
                        Description = (embeddedDescription ?? instance.Description)?.CleanDescription() ?? string.Empty,
                        Version = version,
                        Authors = authors,
                        RepositoryUrl = repositoryUrl,
                        HomepageUrl = homepageUrl,
                        Tags = tags,
                        InstalledAt = createdAt,
                        IsPinned = string.IsNullOrEmpty(dirPath)
                            ? File.Exists(Path.ChangeExtension(dllPath, Pinned))
                            : File.Exists(Path.Join(dirPath, Pinned)),
                        IsEnabled = settings.Plugins.EnabledPlugins[name],
                        ContainingDirectory = dirPath,
                        Priority = settings.Plugins.Priority.IndexOf(name),
                        CanLoad = version.RuntimeIdentifier is IPluginManager.AnyRuntimeIdentifier || version.RuntimeIdentifier == RuntimeIdentifier,
                        CannotLoadReason = version.RuntimeIdentifier is IPluginManager.AnyRuntimeIdentifier || version.RuntimeIdentifier == RuntimeIdentifier
                            ? null
                            : $"The plugin is built for the \"{version.RuntimeIdentifier}\" runtime, not this server's \"{RuntimeIdentifier}\".",
                        CanUninstall = !isSystem,
                        DLLs = [dllPath, .. dlls.Except([dllPath])],
                        Thumbnail = thumbnailImage,
                        Icon = iconImage,
                        Dependencies = embeddedDependencies
                            .Select(dependency => new PluginDependency
                            {
                                PluginID = dependency.PluginID,
                                VersionRange = dependency.VersionRange,
                                IsOptional = dependency.IsOptional,
                            })
                            .ToList(),
                    };
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to check assembly {Name} for valid IPlugin and IPluginServiceRegistration implementations; {DllPath}", name, dllPath);
                    break;
                }
            }
        }
        finally
        {
            alc.Unload();
        }

        return null;
    }

    #region Setup | Version

    private const string RepositoryUrl = "RepositoryUrl";

    private const string PackageID = "PackageID";

    private const string PackageName = "PackageName";

    private const string PackageOverview = "PackageOverview";

    private const string PackageDependencies = "PackageDependencies";

    private const string PackageProjectUrl = "PackageProjectUrl";

    private const string PackageTags = "PackageTags";

    private const string NewRuntimeIdentifier = "RuntimeIdentifier";

    private const string NewReleaseTag = "ReleaseTag";

    private const string NewSourceRevision = "SourceRevision";

    private const string NewReleaseDate = "ReleaseDate";

    private const string NewReleaseChannel = "ReleaseChannel";

    private const string LegacyRuntimeIdentifier = "runtime";

    private const string LegacyReleaseTag = "tag";

    private const string LegacySourceRevision = "commit";

    private const string LegacyReleaseChannel = "channel";

    public const string LegacyReleaseDate = "date";

    private static VersionInformation? _serverVersionInformation;

    internal static VersionInformation GetVersionInformation()
        => _serverVersionInformation ??= ReadVersionInformationFromAssembly(Assembly.GetExecutingAssembly(), out _, out _)!;

    internal static VersionInformation? GetVersionInformation(Assembly assembly)
        => ReadVersionInformationFromAssembly(assembly, out _, out _)!;

    private static VersionInformation? ReadVersionInformationFromAssembly(Assembly assembly, out bool isLegacyNamespace, out Dictionary<string, string?> metadataAttributeDict)
    {
        var dllPath = assembly.Location!;
        var referencedAssemblies = assembly.GetReferencedAssemblies();
        var legacyRef = referencedAssemblies.FirstOrDefault(r => r.Name is "Shoko.Plugin.Abstractions");
        var newRef = referencedAssemblies.FirstOrDefault(r => r.Name is "Shoko.Abstractions");
        isLegacyNamespace = legacyRef is not null && newRef is null;
        metadataAttributeDict = [];
        var abstractionVersion = (isLegacyNamespace ? legacyRef : newRef)?.Version is { Major: var abiMajor, Minor: var abiMinor, Build: var abiBuild }
            ? new Version(abiMajor, abiMinor, abiBuild)
            : new(0, 0, 0);
        // Silently skip DLLs which doesn't reference the abstraction.
        if (abstractionVersion <= _invalidVersion)
            return null;

        var version = assembly.GetName().Version is { Major: var assMajor, Minor: var assMinor, Build: var assBuild, Revision: var assRevision }
            ? assRevision is <= 0 ? new(assMajor, assMinor, assBuild) : new(assMajor, assMinor, assBuild, assRevision)
            : new Version(0, 0, 0, 0);
        if (version <= _invalidVersion)
            return null;

        metadataAttributeDict = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(a => KeyValuePair.Create(a.Key, a.Value))
            .DistinctBy(kp => kp.Key)
            .ToDictionary();
        var extraVersionDict = GetApplicationExtraVersion(assembly);

        string runtime;
        if (metadataAttributeDict.ContainsKey(NewRuntimeIdentifier) && !string.IsNullOrEmpty(metadataAttributeDict[NewRuntimeIdentifier]))
            runtime = metadataAttributeDict[NewRuntimeIdentifier]!;
        else if (extraVersionDict.ContainsKey(NewRuntimeIdentifier) && !string.IsNullOrEmpty(extraVersionDict[NewRuntimeIdentifier]))
            runtime = extraVersionDict[NewRuntimeIdentifier];
        else if (extraVersionDict.ContainsKey(LegacyRuntimeIdentifier) && !string.IsNullOrEmpty(extraVersionDict[LegacyRuntimeIdentifier]))
            runtime = extraVersionDict[LegacyRuntimeIdentifier];
        else
            runtime = "any";

        var tag = (string?)null;
        if (metadataAttributeDict.ContainsKey(NewReleaseTag) && !string.IsNullOrEmpty(metadataAttributeDict[NewReleaseTag]))
            tag = metadataAttributeDict[NewReleaseTag];
        else if (extraVersionDict.ContainsKey(NewReleaseTag) && !string.IsNullOrEmpty(extraVersionDict[NewReleaseTag]))
            tag = extraVersionDict[NewReleaseTag];
        else if (extraVersionDict.ContainsKey(LegacyReleaseTag) && !string.IsNullOrEmpty(extraVersionDict[LegacyReleaseTag]))
            tag = extraVersionDict[LegacyReleaseTag];

        var sourceRevision = (string?)null;
        if (metadataAttributeDict.ContainsKey(NewSourceRevision) && !string.IsNullOrEmpty(metadataAttributeDict[NewSourceRevision]))
            sourceRevision = metadataAttributeDict[NewSourceRevision];
        else if (extraVersionDict.ContainsKey(NewSourceRevision) && !string.IsNullOrEmpty(extraVersionDict[NewSourceRevision]))
            sourceRevision = extraVersionDict[NewSourceRevision];
        else if (extraVersionDict.ContainsKey(LegacySourceRevision) && !string.IsNullOrEmpty(extraVersionDict[LegacySourceRevision]))
            sourceRevision = extraVersionDict[LegacySourceRevision];

        DateTime releasedAt;
        if (metadataAttributeDict.ContainsKey(NewReleaseDate) && DateTime.TryParse(metadataAttributeDict[NewReleaseDate], out var metaReleasedAt))
            releasedAt = metaReleasedAt.ToUniversalTime();
        else if (extraVersionDict.ContainsKey(NewReleaseDate) && DateTime.TryParse(extraVersionDict[NewReleaseDate], out var extraReleasedAt0))
            releasedAt = extraReleasedAt0.ToUniversalTime();
        else if (extraVersionDict.ContainsKey(LegacyReleaseDate) && DateTime.TryParse(extraVersionDict[LegacyReleaseDate], out var extraReleasedAt1))
            releasedAt = extraReleasedAt1.ToUniversalTime();
        else
            releasedAt = File.GetCreationTimeUtc(dllPath);

        var isDebug = assembly.GetCustomAttribute<DebuggableAttribute>() is { DebuggingFlags: > DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints };
        var releaseChannel = isDebug ? ReleaseChannel.Debug : version.Revision > 0 ? ReleaseChannel.Dev : ReleaseChannel.Stable;
        if (!isDebug)
        {
            if (metadataAttributeDict.ContainsKey(NewReleaseChannel) && !string.IsNullOrEmpty(metadataAttributeDict[NewReleaseChannel]) && Enum.TryParse<ReleaseChannel>(metadataAttributeDict[NewReleaseChannel]!, true, out var metaReleaseChannel))
                releaseChannel = metaReleaseChannel;
            else if (extraVersionDict.ContainsKey(NewReleaseChannel) && !string.IsNullOrEmpty(extraVersionDict[NewReleaseChannel]) && Enum.TryParse<ReleaseChannel>(extraVersionDict[NewReleaseChannel]!, true, out var extraReleaseChannel0))
                releaseChannel = extraReleaseChannel0;
            else if (extraVersionDict.ContainsKey(LegacyReleaseChannel) && !string.IsNullOrEmpty(extraVersionDict[LegacyReleaseChannel]) && Enum.TryParse<ReleaseChannel>(extraVersionDict[LegacyReleaseChannel]!, true, out var extraReleaseChannel1))
                releaseChannel = extraReleaseChannel1;
        }

        return new()
        {
            Version = version,
            RuntimeIdentifier = runtime,
            AbstractionVersion = abstractionVersion,
            SourceRevision = sourceRevision,
            ReleaseTag = tag,
            Channel = releaseChannel,
            ReleasedAt = releasedAt,
        };
    }

    private static Dictionary<string, string> GetApplicationExtraVersion(Assembly assembly)
    {
        if (assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) is not AssemblyInformationalVersionAttribute version)
            return [];

        return version.InformationalVersion.Split(",")
                .Select(raw => raw.Split("="))
                .Where(pair => pair.Length == 2 && !string.IsNullOrEmpty(pair[1]))
                .ToDictionary(pair => pair[0], pair => pair[1]);
    }

    #endregion

    #endregion

    #region Plugin Info

    public IReadOnlyList<LocalPluginInfo> GetPluginInfos()
        => _pluginTypes;

    public IReadOnlyList<LocalPluginInfo> GetPluginInfos(Guid pluginId)
        => _pluginTypes.Where(p => p.ID == pluginId).ToList();

    public LocalPluginInfo? GetPluginInfo(Guid pluginId, Version? pluginVersion = null)
        => pluginVersion is not null
            ? _pluginTypes.FirstOrDefault(p => p.ID == pluginId && p.Version.Version == pluginVersion)
            : _pluginTypes.FirstOrDefault(p => p.ID == pluginId && p.IsActive) ?? _pluginTypes.Where(p => p.ID == pluginId).OrderByDescending(p => p.Version).FirstOrDefault();

    public LocalPluginInfo? GetPluginInfo(IPlugin plugin)
        => _pluginTypes.FirstOrDefault(p => p.Plugin is not null && ReferenceEquals(plugin, p.Plugin));

    public LocalPluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin
        => _pluginTypes.FirstOrDefault(p => typeof(TPlugin) == p.PluginType);

    public LocalPluginInfo? GetPluginInfo(Type type)
        => _pluginTypes.FirstOrDefault(p => type == p.PluginType);

    public LocalPluginInfo? GetPluginInfo(Assembly assembly)
        => assembly.GetTypes().Where(type => typeof(IPlugin).IsAssignableFrom(type)).FirstOrDefault() is { } pluginType
            ? GetPluginInfo(pluginType)
            : null;

    /// <summary>
    ///   Finds the active plugin whose code the given assembly is part of:
    ///   the core plugin for the server's own assembly, and otherwise the
    ///   plugin loaded from where the assembly was loaded from.
    /// </summary>
    /// <remarks>
    ///   Plugins are loaded into the default load context, next to the server
    ///   and every shared library, so an assembly there belongs to the plugin
    ///   whose main assembly it is, or whose directory or DLLs it was loaded
    ///   from. The answer is kept per assembly once plugins are loaded. An
    ///   assembly in any other load context belongs to the plugin loaded into
    ///   that context.
    /// </remarks>
    /// <param name="assembly">The assembly to look up.</param>
    /// <returns>
    ///   The owning plugin, or <see langword="null"/> for shared assemblies:
    ///   the runtime, the framework and the abstractions wherever they were
    ///   loaded from, and any library loaded from outside the plugins.
    /// </returns>
    internal LocalPluginInfo? GetOwningPluginInfo(Assembly assembly)
    {
        if (assembly == typeof(CorePlugin).Assembly)
            return GetPluginInfo(CorePlugin.StaticID);

        var loadContext = AssemblyLoadContext.GetLoadContext(assembly);
        if (loadContext is null)
            return null;

        var pluginInfos = _pluginTypes.ToArray();
        if (loadContext != AssemblyLoadContext.Default)
            return pluginInfos.FirstOrDefault(pluginInfo => pluginInfo is { IsActive: true, PluginType: { } pluginType }
                && AssemblyLoadContext.GetLoadContext(pluginType.Assembly) == loadContext);

        var pluginId = _pluginsLoaded
            ? _assemblyOwners.GetOrAdd(assembly, static (assembly, pluginInfos) => FindLoadedPlugin(assembly, pluginInfos)?.ID, pluginInfos)
            : FindLoadedPlugin(assembly, pluginInfos)?.ID;
        return pluginId is { } id
            ? pluginInfos.FirstOrDefault(pluginInfo => pluginInfo.IsActive && pluginInfo.ID == id)
            : null;
    }

    /// <summary>
    ///   Finds the loaded plugin, other than the core plugin, that an
    ///   assembly in the default load context belongs to.
    /// </summary>
    /// <param name="assembly">The assembly to look up.</param>
    /// <param name="pluginInfos">The known plugins, loaded or not.</param>
    /// <returns>
    ///   The plugin whose main assembly it is, or whose directory or DLLs it
    ///   was loaded from, or <see langword="null"/> for an assembly passed
    ///   owned by no plugin (see <see cref="IsPassThrough"/>) or loaded from elsewhere.
    /// </returns>
    private static LocalPluginInfo? FindLoadedPlugin(Assembly assembly, IReadOnlyList<LocalPluginInfo> pluginInfos)
    {
        if (IsPassThrough(assembly))
            return null;

        var loaded = pluginInfos
            .Where(pluginInfo => pluginInfo.PluginType is not null && pluginInfo.ID != CorePlugin.StaticID)
            .ToList();
        return loaded.FirstOrDefault(pluginInfo => pluginInfo.PluginType!.Assembly == assembly)
            ?? FindPluginByLocation(assembly.Location, loaded);
    }

    /// <summary>
    ///   Finds the plugin a file was loaded from: the plugin whose directory
    ///   holds it, or, for a plugin without one, the plugin listing it among
    ///   its DLLs.
    /// </summary>
    /// <param name="location">The path the assembly was loaded from. Empty for an assembly loaded from memory.</param>
    /// <param name="pluginInfos">The plugins to look in.</param>
    /// <returns>The plugin, or <see langword="null"/> if none holds the file.</returns>
    internal static LocalPluginInfo? FindPluginByLocation(string location, IEnumerable<LocalPluginInfo> pluginInfos)
    {
        if (string.IsNullOrEmpty(location))
            return null;

        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var path = Path.GetFullPath(location);
        foreach (var pluginInfo in pluginInfos)
        {
            if (pluginInfo.ContainingDirectory is { Length: > 0 } directory)
            {
                var directoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
                if (path.StartsWith(directoryPath, comparison))
                    return pluginInfo;
            }

            if (pluginInfo.DLLs.Any(dll => !string.IsNullOrEmpty(dll) && string.Equals(Path.GetFullPath(dll), path, comparison)))
                return pluginInfo;
        }

        return null;
    }

    /// <summary>
    ///   Tells whether an assembly is shared code no plugin owns: the runtime,
    ///   the framework or the abstractions, wherever it was loaded from.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns><see langword="true"/> if no plugin owns it.</returns>
    internal static bool IsPassThrough(Assembly assembly)
    {
        if (assembly == typeof(IPlugin).Assembly)
            return true;

        var name = assembly.GetName().Name ?? string.Empty;
        return name is "System" or "mscorlib" or "netstandard"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }

    #endregion

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
    ///   disabled, pinned, unpinned or uninstalled, so the restart reasons can
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
            ReapplyDependencyGraph(_pluginTypes, _dependencyRefusals, logger);

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

    private LocalPluginInfo WithStateChanged(LocalPluginInfo pluginInfo)
    {
        OnStateChanged();
        return pluginInfo;
    }

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
                    MarkPluginDataForRemoval(pluginInfo.ID);
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
    /// <returns><see langword="true"/> when nothing is left of the plugin in the core's database.</returns>
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
            Thumbnail = LoadPluginImageInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail, ThumbnailKind),
            Icon = LoadPluginImageInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Icon, IconKind),
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

    /// <summary>
    ///   Read an image a plugin embedded in its own assembly.
    /// </summary>
    /// <param name="assembly">The plugin assembly to read from.</param>
    /// <param name="assemblyName">
    ///   The assembly's name. A resource name not rooted in it is refused,
    ///   since a plugin may only name its own resources.
    /// </param>
    /// <param name="resourceName">The resource name the plugin advertised, if any.</param>
    /// <param name="kind">Which image this is, for the log line.</param>
    /// <param name="dllPath">The dll being read, for the log line.</param>
    /// <returns>
    ///   The image bytes, or <see langword="null"/> when the plugin advertised
    ///   none, named something outside its own assembly, or the read failed.
    /// </returns>
    private byte[]? ReadEmbeddedImage(Assembly assembly, string assemblyName, string? resourceName, string kind, string dllPath)
    {
        if (resourceName is not { Length: > 0 } || !resourceName.StartsWith(assemblyName + "."))
            return null;

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                logger.LogInformation("Failed to load {Kind} for {DllName}", kind, dllPath);
                return null;
            }

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load {Kind} for {DllName}", kind, dllPath);
            return null;
        }
    }

    /// <summary>
    ///   Find a plugin's image of the given kind: a file shipped beside the
    ///   plugin, or the bytes it embedded, which are written out so both cases
    ///   end up being served from a path.
    /// </summary>
    /// <param name="containingDirectory">
    ///   The plugin's own directory, when it has one. A plugin installed as a
    ///   loose dll does not, and names its files after the dll instead.
    /// </param>
    /// <param name="dll">The plugin's main dll.</param>
    /// <param name="imageBytes">The embedded bytes, when the plugin supplied any.</param>
    /// <param name="kind">
    ///   <see cref="ThumbnailKind"/> or <see cref="IconKind"/>, which is both
    ///   the file name looked for and the one written.
    /// </param>
    /// <returns>The image, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="IOException">
    ///   The embedded bytes could not be written beside the plugin.
    /// </exception>
    private PackageImageInfo? LoadPluginImageInfo(string? containingDirectory, string dll, byte[]? imageBytes, string kind)
    {
        var hasDirectory = !string.IsNullOrEmpty(containingDirectory);
        var directory = hasDirectory ? containingDirectory! : Path.GetDirectoryName(dll)!;
        var pattern = hasDirectory ? kind + ".*" : Path.ChangeExtension(Path.GetFileName(dll), "." + kind + ".*");
        foreach (var fileName in Directory.EnumerateFiles(directory, pattern, new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = false }))
        {
            if (!ContentTypeHelper.TryGetContentType(fileName, out _))
                continue;

            var existing = new MagickImageInfo(fileName);
            if (GetMimeFromFormat(existing) is not { } existingMime)
                continue;

            return ToImageInfo(fileName, (int)existing.Width, (int)existing.Height, existingMime);
        }

        if (imageBytes is not { Length: > 8 })
            return null;

        var imageInfo = new MagickImageInfo(imageBytes);
        if (GetMimeFromFormat(imageInfo) is not { } mime)
            return null;

        if (!ContentTypeHelper.TryGetExtensionForMimeType(mime, out var extName))
            return null;

        var targetName = hasDirectory
            ? Path.Combine(containingDirectory!, kind + extName)
            : Path.ChangeExtension(dll, "." + kind + extName);
        File.WriteAllBytes(targetName, imageBytes);

        return ToImageInfo(targetName, (int)imageInfo.Width, (int)imageInfo.Height, mime);
    }

    private PackageImageInfo ToImageInfo(string fileName, int width, int height, string mime)
        => new()
        {
            Height = height,
            Width = width,
            FilePath = fileName
                .Replace(applicationPaths.PluginsPath, "%PluginsPath%")
                .Replace(applicationPaths.ApplicationPath, "%ApplicationPaths%"),
            MimeType = mime,
        };

    internal static string? GetMimeFromFormat(MagickImageInfo imageInfo)
        => imageInfo.Format switch
        {
            MagickFormat.Png => "image/png",
            MagickFormat.Png00 => "image/png",
            MagickFormat.Png8 => "image/png",
            MagickFormat.Png24 => "image/png",
            MagickFormat.Png32 => "image/png",
            MagickFormat.Png48 => "image/png",
            MagickFormat.Png64 => "image/png",
            MagickFormat.Jpg => "image/jpeg",
            MagickFormat.Jpeg => "image/jpeg",
            MagickFormat.WebP => "image/webp",
            MagickFormat.Svg => "image/svg+xml",
            MagickFormat.Svgz => "image/svg+xml",
            _ => null,
        };

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

    #endregion

    #region Types & Exports

    public IEnumerable<Type> GetTypes<T>()
        => _exportedTypes.Where(IsConcreteTypeOf<T>);

    public IEnumerable<Type> GetTypes<T>(IPlugin plugin)
        => GetPluginInfo(plugin) is { IsActive: true } pluginInfo
            ? pluginInfo.Types.Where(IsConcreteTypeOf<T>)
            : [];

    /// <summary>
    ///   Checks if <paramref name="type"/> is a closed, non-abstract class assignable to
    ///   <typeparamref name="T"/>, so an instance of it can be created.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <param name="type">
    ///   The type to check.
    /// </param>
    /// <returns>
    ///   <c>true</c> if <paramref name="type"/> can be created as a <typeparamref name="T"/>.
    /// </returns>
    private static bool IsConcreteTypeOf<T>(Type type)
        => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && typeof(T).IsAssignableFrom(type);

    public T? GetExport<T>(Type type)
        => !typeof(T).IsAssignableFrom(type) ? default : typeof(T).IsValueType ? (T?)Activator.CreateInstance(type) : (T?)ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, type);

    public IEnumerable<T> GetExports<T>()
        => GetTypes<T>()
            .Select(t =>
            {
                try
                {
                    return ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, t);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to initialize instance of type {TypeName}", t.FullName);
                    return null;
                }
            })
            .WhereNotNull()
            .Cast<T>();

    public IEnumerable<T> GetExports<T>(IPlugin plugin)
        => GetTypes<T>(plugin)
            .Select(t =>
            {
                try
                {
                    return ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, t);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to initialize instance of type {TypeName}", t.FullName);
                    return null;
                }
            })
            .WhereNotNull()
            .Cast<T>();

    public object? GetService(Type type)
        => ISystemService.StaticServices.GetService(type);

    public T? GetService<T>()
        => ISystemService.StaticServices.GetService<T>();

    public object GetRequiredService(Type type)
        => ISystemService.StaticServices.GetRequiredService(type);

    public T GetRequiredService<T>() where T : notnull
        => ISystemService.StaticServices.GetRequiredService<T>();

    #endregion
}
