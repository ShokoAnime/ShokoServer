using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Abstractions.Web;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.API.Swagger;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Watchdog;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;

#pragma warning disable CS0618
namespace Shoko.Server.Plugin;

public partial class PluginManager(ILogger<PluginManager> logger, ISystemService systemService, IApplicationPaths applicationPaths) : IPluginManager, IInstalledPluginReader
{
    private const string Remove = ".remove";

    private const string Pinned = ".pinned";

    private const string Keep = ".keep";

    private const string Dependency = ".dependency";

    private readonly List<Type> _exportedTypes = [];

    private readonly List<LocalPluginInfo> _pluginTypes = [];

    /// <summary>
    ///   The ID of the plugin owning each assembly in the default load
    ///   context, or <c>null</c> for one no plugin owns. Only
    ///   filled once <see cref="ScanForPlugins"/> has loaded every plugin.
    /// </summary>
    private readonly ConcurrentDictionary<Assembly, Guid?> _assemblyOwners = new();

    /// <summary>
    ///   The load state each plugin refused for its dependencies had before
    ///   it was refused, so the refusal can be taken back once they are
    ///   satisfied again.
    /// </summary>
    private readonly Dictionary<LocalPluginInfo, PluginLoadState> _dependencyRefusals = [];

    /// <summary>
    ///   The load context of each loaded plugin, by its ID. The core has none:
    ///   it lives in the server's own.
    /// </summary>
    private readonly Dictionary<Guid, PluginLoadContext> _loadContexts = [];

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

    /// <summary>
    ///   Basic information about a plugin, used during initial loading before
    ///   the full <see cref="LocalPluginInfo"/> is available.
    /// </summary>
    internal sealed class InternalPluginInfo
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
        ///   The namespace the plugin claims, if it claims a valid one.
        /// </summary>
        public string? Namespace { get; init; }

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
        ///   When the plugin version was installed, the last-write time of its
        ///   main DLL.
        /// </summary>
        public required DateTime InstalledAt { get; init; }

        /// <summary>
        ///   Indicates the plugin is pinned and should not be automatically updated
        ///   or reordered based on version.
        /// </summary>
        public required bool IsPinned { get; init; }

        /// <summary>
        ///   Indicates a <c>.keep</c> file protects the plugin version from
        ///   the purge of inactive versions.
        /// </summary>
        public bool IsKept { get; init; }

        /// <summary>
        ///   Indicates the plugin is enabled and should be loaded. Set again
        ///   by <see cref="ApplyDemand"/> for a plugin enabled on demand.
        /// </summary>
        public required bool IsEnabled { get; set; }

        /// <summary>
        ///   Indicates a <c>.dependency</c> file marks the version as installed
        ///   as a dependency.
        /// </summary>
        public bool IsDependency { get; init; }

        /// <summary>
        ///   Indicates the plugin has no enabled state of its own and follows
        ///   its dependents. Set by <see cref="ApplyDemand"/>.
        /// </summary>
        public bool IsEnabledOnDemand { get; set; }

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
        ///   Indicates the plugin was built for the legacy
        ///   <c>Shoko.Plugin.Abstractions</c>. Its ID is made up from its name
        ///   and version, so an installed build of the same assembly for this
        ///   server replaces it.
        /// </summary>
        public bool IsLegacy { get; init; }

        /// <summary>
        ///   Why the plugin failed while it was looked at, if it did. An
        ///   enabled plugin with one is not loaded and fails the start.
        /// </summary>
        public string? FailureReason { get; init; }

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

    /// <summary>
    ///   Gets the <c>.pinned</c> file of a plugin version, named the way
    ///   <see cref="GetKeepFile"/> names the <c>.keep</c> file.
    /// </summary>
    /// <param name="directory">The plugin's directory, if it has one.</param>
    /// <param name="dll">The plugin's main DLL.</param>
    /// <returns>The path of the <c>.pinned</c> file.</returns>
    private static string GetPinnedFile(string? directory, string dll)
        => string.IsNullOrEmpty(directory)
            ? Path.ChangeExtension(dll, Pinned)
            : Path.Join(directory, Pinned);

    /// <summary>
    ///   Gets the <c>.keep</c> file of a plugin version: <c>.keep</c> in its
    ///   directory, or <c>&lt;dll&gt;.keep</c> next to a single-DLL plugin.
    /// </summary>
    /// <param name="directory">The plugin's directory, if it has one.</param>
    /// <param name="dll">The plugin's main DLL.</param>
    /// <returns>The path of the <c>.keep</c> file.</returns>
    internal static string GetKeepFile(string? directory, string dll)
        => string.IsNullOrEmpty(directory)
            ? Path.ChangeExtension(dll, Keep)
            : Path.Join(directory, Keep);

    /// <summary>
    ///   Gets the <c>.remove</c> file marking a plugin version for removal
    ///   on the next start, named the way <see cref="GetKeepFile"/> is.
    /// </summary>
    /// <param name="directory">The plugin's directory, if it has one.</param>
    /// <param name="dll">The plugin's main DLL.</param>
    /// <returns>The path of the <c>.remove</c> file.</returns>
    private static string GetRemovalFile(string? directory, string dll)
        => string.IsNullOrEmpty(directory)
            ? Path.ChangeExtension(dll, Remove)
            : Path.Join(directory, Remove);

    /// <summary>
    ///   Gets the <c>.dependency</c> file marking a plugin version as installed
    ///   as a dependency, named the way <see cref="GetKeepFile"/> is.
    /// </summary>
    /// <param name="directory">The plugin's directory, if it has one.</param>
    /// <param name="dll">The plugin's main DLL.</param>
    /// <returns>The path of the <c>.dependency</c> file.</returns>
    internal static string GetDependencyFile(string? directory, string dll)
        => string.IsNullOrEmpty(directory)
            ? Path.ChangeExtension(dll, Dependency)
            : Path.Join(directory, Dependency);

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
            var before = serviceCollection.ToArray();
            try
            {
                pluginInfo.ServiceRegistrationType!
                    .GetMethod(nameof(IPluginServiceRegistration.RegisterServices), BindingFlags.Public | BindingFlags.Static, [typeof(IServiceCollection), typeof(IApplicationPaths)])!
                    .Invoke(null, [serviceCollection, applicationPaths]);
            }
            catch (Exception ex) when (pluginInfo.ID != CorePlugin.StaticID)
            {
                // Put back as it was, so half a plugin's services are not left in the container.
                serviceCollection.Clear();
                foreach (var descriptor in before)
                    serviceCollection.Add(descriptor);
                MarkFailed(pluginInfo, "registering its services", ex);
            }
        }

        // The databases plugins asked for become contexts on the provider the core picks.
        PluginDatabaseRegistrar.AddPluginDatabases(serviceCollection);

        // The settings clients the core and the plugins registered, refusing a name registered twice.
        UserConfigurationRegistry.AddUserConfigurationRegistry(serviceCollection);

        // Scan every loaded plugin assembly for IQueueJob implementations.
        // Plugins don't need to call AddQueueJobsFromAssembly themselves.
        foreach (var pluginInfo in _pluginTypes.Where(a => a is { CanLoad: true, PluginType: not null }))
        {
            var assembly = pluginInfo.PluginType!.Assembly;
            if (assembly == typeof(PluginManager).Assembly)
                continue; //Skip the current assembly, as this is registered implicitly.

            logger.LogTrace("Scanning plugin assembly for queue jobs. ({DllName})", Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]));
            try
            {
                serviceCollection.AddQueueJobsFromAssembly(pluginInfo.PluginType!.Assembly);
            }
            catch (Exception ex)
            {
                MarkFailed(pluginInfo, "registering its queue jobs", ex);
            }
        }

        // One job type per metadata provider, so each can be held back and limited alone; the closed
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

        // One job type per release provider without a job class of its own, likewise.
        foreach (var overlong in ReleaseProviderJobs.GetOverlongJobTypes(providerTypes))
            logger.LogWarning(
                "Not registering {JobType}: its stored name is longer than the queue keeps, so its jobs cannot be queued.",
                JobTypeNames.Short(overlong)
            );

        // One job type per image contributor too, so each gets a limit of its own.
        foreach (var contributorType in providerTypes)
            if (MetadataImageContributorJobs.GetOverlongJobType(contributorType) is { } overlong)
                logger.LogWarning(
                    "Not registering {JobType} for {Contributor}: its stored name is longer than the queue keeps, so its jobs cannot be queued.",
                    JobTypeNames.Short(overlong), contributorType.FullName
                );

        // And one per airing schedule provider, likewise.
        foreach (var providerType in providerTypes)
            foreach (var overlong in AiringScheduleProviderJobs.GetOverlongJobTypes(providerType))
                logger.LogWarning(
                    "Not registering {JobType} for {Provider}: its stored name is longer than the queue keeps, so its jobs cannot be queued.",
                    JobTypeNames.Short(overlong), providerType.FullName
                );

        var providerJobTypes = providerTypes
            .SelectMany(MetadataProviderJobs.GetJobTypes)
            .Concat(providerTypes.Select(MetadataImageContributorJobs.GetJobType).OfType<Type>())
            .Concat(ReleaseProviderJobs.GetJobTypes(providerTypes))
            .Concat(providerTypes.SelectMany(AiringScheduleProviderJobs.GetJobTypes))
            .ToList();
        if (providerJobTypes.Count > 0)
        {
            logger.LogTrace("Registering {Count} metadata, release and airing schedule provider and image contributor job types.", providerJobTypes.Count);
            serviceCollection.AddQueueJobTypes(providerJobTypes);
        }

        // Each sweep job type is watched against the sweep budget rather than the global threshold.
        foreach (var sweepJobType in providerTypes.Select(AiringScheduleProviderJobs.GetSweepJobType).OfType<Type>())
            serviceCollection.AddSingleton<IJobWatchdogThreshold>(
                services => new AiringScheduleSweepWatchdogThreshold(services.GetRequiredService<AiringScheduleService>(), sweepJobType)
            );

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
            // A plugin that failed an earlier step is not taken through the later ones.
            if (!localPluginInfo.IsActive || localPluginInfo.HasFailed)
                continue;

            try
            {
                step(localPluginInfo.Plugin);
            }
            catch (Exception ex)
            {
                MarkFailed(localPluginInfo, doing, ex);
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
            IPlugin pluginInstance;
            try
            {
                pluginInstance = (IPlugin)ActivatorUtilities.CreateInstance(ISystemService.StaticServices, pluginType);
            }
            catch (Exception ex) when (localPluginInfo.ID != CorePlugin.StaticID)
            {
                MarkFailed(localPluginInfo, "being created", ex);
                continue;
            }

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
                IsKept = localPluginInfo.IsKept,
                IsDependency = localPluginInfo.IsDependency,
                IsEnabledOnDemand = localPluginInfo.IsEnabledOnDemand,
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
        var unattributed = new List<Exception>();

        // One service failing on a plugin's part does not keep the others from theirs.
        void AddParts(string doing, Action addParts)
        {
            try
            {
                addParts();
            }
            catch (Exception ex)
            {
                if (FindPluginBehind(ex) is { } pluginInfo)
                {
                    MarkFailed(pluginInfo, doing, ex);
                    return;
                }

                logger.LogError(ex, "Failed while {Doing}, and no plugin could be named for it.", doing);
                unattributed.Add(ex);
            }
        }

        AddParts("registering its configurations", () =>
        {
            if (services.GetRequiredService<IConfigurationService>() is ConfigurationService configurationService)
                configurationService.AddParts(GetTypes<IConfiguration>(), (pluginInfo, ex) => MarkFailed(pluginInfo, "registering its configurations", ex));
        });

        AddParts("registering its metadata resolvers", () =>
        {
            if (services.GetRequiredService<IMetadataService>() is MetadataService metadataService)
                metadataService.AddParts(CreateExports<IResourceResolver>(), CreateExports<IMetadataResolver>());
        });

        AddParts("registering its metadata providers", () =>
            services.GetRequiredService<MetadataProviderManager>().AddParts(CreateExports<IMetadataProvider>()));

        AddParts("registering its image contributors", () =>
            services.GetRequiredService<MetadataImageContributorManager>().AddParts(CreateExports<IMetadataImageContributor>()));

        AddParts("registering its ignore rules", () =>
        {
            if (services.GetRequiredService<IVideoService>() is VideoService videoService)
                videoService.AddParts(CreateExports<IManagedFolderIgnoreRule>());
        });

        // Used to store the updated priorities for the providers in the settings file.
        AddParts("registering its release providers", () =>
        {
            if (services.GetRequiredService<IVideoReleaseService>() is VideoReleaseService videoReleaseService)
                videoReleaseService.AddParts(CreateExports<IReleaseInfoProvider>());
        });

        AddParts("registering its hash providers", () =>
        {
            if (services.GetRequiredService<IVideoHashingService>() is VideoHashingService videoHashingService)
                videoHashingService.AddParts(CreateExports<IHashProvider>());
        });

        AddParts("registering its airing schedule providers", () =>
        {
            if (services.GetRequiredService<IAiringScheduleService>() is AiringScheduleService airingScheduleService)
                airingScheduleService.AddParts(CreateExports<IAiringScheduleProvider>());
        });

        AddParts("registering its relocation providers", () =>
        {
            if (services.GetRequiredService<IVideoRelocationService>() is VideoRelocationService relocationService)
                relocationService.AddParts(CreateExports<IRelocationProvider>());
        });

        AddParts("registering its web UIs", () =>
            services.GetRequiredService<WebUIService>().AddParts(
                CreateExports<IWebUIProvider>(),
                (pluginInfo, ex) => MarkFailed(pluginInfo, "registering its web UI", ex)
            ));

        AddParts("registering its suspension providers", () =>
        {
            if (services.GetRequiredService<ISuspensionService>() is SuspensionService suspensionService)
                suspensionService.AddParts(CreateExports<ISuspensionProvider>());
        });

        AddParts("registering its actions", () =>
            services.GetRequiredService<ActionService>().AddParts(GetTypes<IExecutableAction>()
                .Select(type => (GetPluginInfo(type.Assembly)!.ID, type))));

        AddParts("registering its scheduled actions", () =>
            services.GetRequiredService<ScheduledActionRegistry>().AddParts(GetTypes<IScheduledAction>()
                .Select(type => (GetPluginInfo(type.Assembly)!.ID, type))));

        AddParts("registering its scheduled action providers", () =>
            services.GetRequiredService<ScheduledActionRegistry>().AddProviders(CreateExports<IScheduledActionProvider>()));

        AddParts("registering its stream transforms and playback observers", () =>
        {
            if (services.GetRequiredService<IVideoStreamPipelineService>() is VideoStreamPipelineService videoStreamPipelineService)
            {
                videoStreamPipelineService.AddTransformParts(CreateExports<IVideoStreamTransform>());
                videoStreamPipelineService.AddObserverParts(CreateExports<IPlaybackObserver>());
            }
        });

        if (unattributed.Count > 0)
            throw new AggregateException("Failed to hand the services their parts from the plugins.", unattributed);
    }

    #endregion
}
