using System;
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
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Plugin;

public partial class PluginManager(ILogger<PluginManager> logger, ISystemService systemService, IApplicationPaths applicationPaths) : IPluginManager
{
    private const string Remove = ".remove";

    private const string Pinned = ".pinned";

    private readonly List<Type> _exportedTypes = [];

    private readonly List<LocalPluginInfo> _pluginTypes = [];

    private readonly Guid _coreID = typeof(CorePlugin).FullName!.ToUuidV5();

    private static readonly Version _invalidVersion = new(0, 0, 0, 0);

    #region Setup

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

    internal const string AnyRuntimeIdentifier = "any";

    /// <summary>
    ///   Basic information about a plugin, used during initial loading before
    ///   the full <see cref="LocalPluginInfo"/> is available.
    /// </summary>
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
    }

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

        // Add the core plugin to register it's plugin providers.
        var internalPlugins = new List<InternalPluginInfo>()
        {
            new()
            {
                ID = _coreID,
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
        var settings = Utils.SettingsProvider.GetSettings();
        foreach (var (dirPath, dlls, isSystem) in directories)
            if (LoadInternalPluginInfo(dirPath, dlls, isSystem, settings, ref settingsChanged) is { } internalPluginInfo)
                internalPlugins.Add(internalPluginInfo);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (settingsChanged)
            Utils.SettingsProvider.SaveSettings();

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
                        IsActive = false,
                        CanLoad = internalPluginInfo.CanLoad,
                        CanUninstall = internalPluginInfo.CanUninstall,
                        Plugin = null,
                        PluginType = null,
                        ServiceRegistrationType = null,
                        ApplicationRegistrationType = null,
                        ContainingDirectory = internalPluginInfo.ContainingDirectory,
                        DLLs = internalPluginInfo.DLLs,
                        Types = [],
                        Thumbnail = LoadPluginThumbnailInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail),
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
                    IsActive = false,
                    CanLoad = internalPluginInfo.CanLoad,
                    CanUninstall = internalPluginInfo.CanUninstall,
                    Plugin = null,
                    PluginType = types.First(a => a.GetInterfaces().Contains(typeof(IPlugin))),
                    ServiceRegistrationType = types.FirstOrDefault(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration))),
                    ApplicationRegistrationType = types.FirstOrDefault(a => a.GetInterfaces().Contains(typeof(IPluginApplicationRegistration))),
                    ContainingDirectory = internalPluginInfo.ContainingDirectory,
                    DLLs = internalPluginInfo.DLLs,
                    Types = types,
                    Thumbnail = LoadPluginThumbnailInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail),
                });
            }
        }
    }

    public void RegisterPlugins(IServiceCollection serviceCollection)
    {
        // Register the plugins in order of priority & then register their services.
        var registrationPlugins = _pluginTypes
            .Where(a => a.ServiceRegistrationType is not null)
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

            var pluginType = localPluginInfo.PluginType!;
            var pluginInstance = (IPlugin)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, pluginType);
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
                IsActive = true,
                CanLoad = localPluginInfo.CanLoad,
                CanUninstall = localPluginInfo.CanUninstall,
                Plugin = pluginInstance,
                PluginType = pluginType,
                ServiceRegistrationType = localPluginInfo.ServiceRegistrationType,
                ApplicationRegistrationType = localPluginInfo.ApplicationRegistrationType,
                ContainingDirectory = localPluginInfo.ContainingDirectory,
                DLLs = localPluginInfo.DLLs,
                Types = localPluginInfo.Types,
                Thumbnail = localPluginInfo.Thumbnail,
            };
            _exportedTypes.AddRange(localPluginInfo.Types);

            logger.LogInformation("Initialized plugin \"{Name}\". ({DllName}, {Version})", pluginInstance.Name, dllName, localPluginInfo.Version);
        }

        var configurationService = Utils.ServiceContainer.GetRequiredService<IConfigurationService>();
        configurationService.AddParts(GetTypes<IConfiguration>());

        var videoService = Utils.ServiceContainer.GetRequiredService<IVideoService>();
        videoService.AddParts(GetExports<IManagedFolderIgnoreRule>());

        // Used to store the updated priorities for the providers in the settings file.
        var videoReleaseService = Utils.ServiceContainer.GetRequiredService<IVideoReleaseService>();
        videoReleaseService.AddParts(GetExports<IReleaseInfoProvider>());

        var videoHashingService = Utils.ServiceContainer.GetRequiredService<IVideoHashingService>();
        videoHashingService.AddParts(GetExports<IHashProvider>());

        var relocationService = Utils.ServiceContainer.GetRequiredService<IVideoRelocationService>();
        relocationService.AddParts(GetExports<IRelocationProvider>());
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
        var settings = Utils.SettingsProvider.GetSettings();
        var internalPluginInfo = LoadInternalPluginInfo(containingDirectory, dlls, false, settings, ref settingsChanged);
        if (settingsChanged)
            Utils.SettingsProvider.SaveSettings();

        if (internalPluginInfo is not null)
            logger.LogInformation("Loaded inactive plugin \"{Name}\". ({DllName}, {Version})", internalPluginInfo.Name, Path.GetFileNameWithoutExtension(internalPluginInfo.DLLs[0]), internalPluginInfo.Version);
        return internalPluginInfo;
    }

    private InternalPluginInfo? LoadInternalPluginInfo(string? dirPath, string[] dlls, bool isSystem, IServerSettings settings, ref bool settingsChanged)
    {
        var selfResolvingPluginPath = dlls.FirstOrDefault(dll => Path.Exists(Path.ChangeExtension(dll, ".deps.json")));
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
                        continue;

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

                        return new()
                        {
                            // Create an unique ID for this specific version that failed to load.
                            ID = UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = name,
                            Description = isLegacyNamespace
                                ? "This plugin uses the deprecated Shoko.Plugin.Abstractions namespace and is incompatible with this version of Shoko Server. Please update the plugin to use Shoko.Abstractions."
                                : "The plugin failed to load due to missing dependencies.",
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
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
                        };
                    }

                    if (version.AbstractionVersion > AbstractionVersion)
                    {
                        logger.LogInformation("Skipping DLL because the loaded assembly references a newer version of Shoko.Abstractions than what this server supports; {DllPath}", dllPath);
                        return new()
                        {
                            // Create an unique ID for this specific version that failed to load.
                            ID = UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = name,
                            Description = "The plugin failed to load because it references a newer version of Shoko.Abstractions than what this server supports.",
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
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
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
                            // Create an unique ID for this specific version that failed to load.
                            ID = UuidUtility.GetV5($"{name}@{version}"),
                            DllName = name,
                            Name = name,
                            Description = "The plugin failed to load due to missing dependencies.",
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
                            CanUninstall = !isSystem,
                            DLLs = [dllPath, .. dlls.Except([dllPath])],
                            Thumbnail = null,
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
                    if (instance.ID == _coreID)
                    {
                        logger.LogWarning("Skipping {DllName} because it has the same ID as the core plugin.", dllPath);
                        continue;
                    }
                    var thumbnailImage = (byte[]?)null;
                    if (instance.EmbeddedThumbnailResourceName is { Length: > 0 } thumbnailResourceName && thumbnailResourceName.StartsWith(assemblyName + "."))
                    {
                        try
                        {
                            using var thumbnailStream = assembly.GetManifestResourceStream(thumbnailResourceName);
                            if (thumbnailStream is null)
                            {
                                logger.LogInformation("Failed to load thumbnail for {DllName}", dllPath);
                            }
                            else
                            {
                                var thumbnailImage0 = new byte[thumbnailStream.Length];
                                thumbnailStream.ReadExactly(thumbnailImage0);
                                thumbnailImage = thumbnailImage0;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to load thumbnail for {DllName}", dllPath);
                        }
                    }
                    return new()
                    {
                        ID = instance.ID,
                        DllName = name,
                        Name = instance.Name,
                        Description = instance.Description?.CleanDescription() ?? string.Empty,
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
                        CanLoad = version.RuntimeIdentifier is AnyRuntimeIdentifier || version.RuntimeIdentifier == RuntimeIdentifier,
                        CanUninstall = !isSystem,
                        DLLs = [dllPath, .. dlls.Except([dllPath])],
                        Thumbnail = thumbnailImage,
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

    private const string PackageProjectUrl = "PackageProjectUrl";

    private const string PackageTags = "PackageTags";

    private const string NewRuntimeIdentifier = "RuntimeIdentifier";

    private const string NewReleaseTag = "ReleaseTag";

    private const string NewSourceRevision = "SourceRevision";

    private const string NewReleaseDate = "ReleaseDate";

    private const string LegacyRuntimeIdentifier = "runtime";

    private const string LegacyReleaseTag = "tag";

    private const string LegacySourceRevision = "commit";

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

    #endregion

    #region Plugin Management

    /// <inheritdoc/>
    public event EventHandler<PluginInstallationEventArgs>? PluginInstalled;

    /// <inheritdoc/>
    public event EventHandler<PluginInstallationEventArgs>? PluginUninstalled;

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

        lock (_pluginTypes)
        {
            if (LoadFromPathInternal(path) is not { } pluginInfo)
                return null;

            Task.Run(() => PluginInstalled?.Invoke(null, new()
            {
                Plugin = pluginInfo,
                OccurredAt = DateTime.UtcNow,
            }));

            return pluginInfo;
        }
    }

    public LocalPluginInfo EnablePlugin(LocalPluginInfo pluginInfo)
        => TogglePlugin(pluginInfo, true);

    public LocalPluginInfo DisablePlugin(LocalPluginInfo pluginInfo)
        => TogglePlugin(pluginInfo, false);

    public LocalPluginInfo UninstallPlugin(LocalPluginInfo pluginInfo, bool purgeConfiguration = true)
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
            var settings = Utils.SettingsProvider.GetSettings();
            if (settings.Plugins.EnabledPlugins.Remove(dllName))
                Utils.SettingsProvider.SaveSettings(settings);

            // Purge configuration if requested.
            if (purgeConfiguration)
            {
                // Remove the default plugin config directory if it exists.
                var pluginConfigDir = Path.Join(applicationPaths.ConfigurationsPath, pluginInfo.ID.ToString());
                if (Directory.Exists(pluginConfigDir))
                    Directory.Delete(pluginConfigDir, true);

                // Remove any configuration files outside the default plugin config directory if we have the plugin loaded.
                if (pluginInfo.Plugin is not null)
                {
                    pluginConfigDir += Path.DirectorySeparatorChar;
                    var configurationService = Utils.ServiceContainer.GetRequiredService<IConfigurationService>();
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

            Task.Run(() => PluginUninstalled?.Invoke(null, new()
            {
                Plugin = pluginInfo,
                OccurredAt = DateTime.UtcNow,
            }));

            return pluginInfo;
        }
    }

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
        if (!string.IsNullOrEmpty(containingDirectory))
        {
            if (_pluginTypes.Find(p => p.IsInstalled && p.ContainingDirectory == containingDirectory) is { } existingPluginInfo)
                return existingPluginInfo;
        }
        else if (dlls.Length is 1)
        {
            if (_pluginTypes.Find(p => p.IsInstalled && p.ContainingDirectory is null && p.DLLs[0] == dlls[0]) is { } existingPluginInfo)
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
            IsActive = false,
            CanLoad = internalPluginInfo.CanLoad,
            CanUninstall = internalPluginInfo.CanUninstall,
            Plugin = null,
            PluginType = null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = internalPluginInfo.ContainingDirectory,
            DLLs = internalPluginInfo.DLLs,
            Types = [],
            Thumbnail = LoadPluginThumbnailInfo(internalPluginInfo.ContainingDirectory, internalPluginInfo.DLLs[0], internalPluginInfo.Thumbnail),
        };
        _pluginTypes.Add(pluginInfo);
        return pluginInfo;
    }

    private PackageThumbnailInfo? LoadPluginThumbnailInfo(string? containingDirectory, string dll, byte[]? thumbnailBytes)
    {
        if (!string.IsNullOrEmpty(containingDirectory))
        {
            foreach (var fileName in Directory.EnumerateFiles(containingDirectory, "thumbnail.*", new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = false }))
            {
                var mime = MimeMapping.MimeUtility.GetMimeMapping(Path.GetExtension(fileName));
                if (mime is not null and not "application/octet-stream")
                {
                    var imageInfo = new MagickImageInfo(fileName);
                    mime = GetMimeFromFormat(imageInfo);
                    if (mime is null)
                        continue;

                    return new()
                    {
                        Height = imageInfo.Height,
                        Width = imageInfo.Width,
                        FilePath = fileName
                            .Replace(applicationPaths.PluginsPath, "%PluginsPath%")
                            .Replace(applicationPaths.ApplicationPath, "%ApplicationPaths%"),
                        MimeType = mime,
                    };
                }
            }
        }
        else
        {
            var thumbnailFile = Path.ChangeExtension(Path.GetFileName(dll), ".thumbnail.*");
            foreach (var fileName in Directory.EnumerateFiles(Path.GetDirectoryName(dll)!, thumbnailFile, new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = false }))
            {
                var mime = MimeMapping.MimeUtility.GetMimeMapping(Path.GetExtension(fileName));
                if (mime is not null and not "application/octet-stream")
                {
                    var imageInfo = new MagickImageInfo(fileName);
                    mime = GetMimeFromFormat(imageInfo);
                    if (mime is null)
                        continue;

                    return new()
                    {
                        Height = imageInfo.Height,
                        Width = imageInfo.Width,
                        FilePath = fileName
                            .Replace(applicationPaths.PluginsPath, "%PluginsPath%")
                            .Replace(applicationPaths.ApplicationPath, "%ApplicationPaths%"),
                        MimeType = mime,
                    };
                }
            }
        }

        if (thumbnailBytes is { Length: > 8 })
        {
            var imageInfo = new MagickImageInfo(thumbnailBytes);
            var mime = GetMimeFromFormat(imageInfo);
            if (mime is null)
                return null;

            var extName = MimeMapping.MimeUtility.GetExtensions(mime)?.FirstOrDefault();
            if (extName is null)
                return null;

            var fileName = Path.ChangeExtension(dll, ".thumbnail" + extName);
            File.WriteAllBytes(fileName, thumbnailBytes);

            return new()
            {
                Height = imageInfo.Height,
                Width = imageInfo.Width,
                FilePath = fileName
                    .Replace(applicationPaths.PluginsPath, "%PluginsPath%")
                    .Replace(applicationPaths.ApplicationPath, "%ApplicationPaths%"),
                MimeType = mime,
            };
        }

        return null;
    }

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
            _ => null,
        };

    private LocalPluginInfo TogglePlugin(LocalPluginInfo pluginInfo, bool enabled)
    {
        var dllName = Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);
        var settings = Utils.SettingsProvider.GetSettings();
        if (enabled)
        {
            if (!pluginInfo.IsInstalled)
                return pluginInfo;

            if ((!settings.Plugins.EnabledPlugins.ContainsKey(dllName)) || !settings.Plugins.EnabledPlugins[dllName])
            {
                settings.Plugins.EnabledPlugins[dllName] = true;
                Utils.SettingsProvider.SaveSettings(settings);
            }
        }
        else
        {
            if (settings.Plugins.EnabledPlugins.TryGetValue(dllName, out var value) && value)
            {
                settings.Plugins.EnabledPlugins[dllName] = false;
                Utils.SettingsProvider.SaveSettings(settings);
            }
        }

        pluginInfo.IsEnabled = enabled;

        // Disable other versions of the same plugin, and pin the version that is enabled if necessary.
        var pluginInfos = _pluginTypes.Where(p => p.ID == pluginInfo.ID).ToList();
        var highestVersion = pluginInfos.MaxBy(p => p.Version);
        if (enabled && highestVersion != pluginInfo)
        {
            var pinnedFile = string.IsNullOrEmpty(pluginInfo.ContainingDirectory)
                ? Path.ChangeExtension(pluginInfo.DLLs[0], Pinned)
                : Path.Join(pluginInfo.ContainingDirectory, Pinned);
            if (!File.Exists(pinnedFile))
                File.WriteAllText(pinnedFile, string.Empty);
            foreach (var plugin in pluginInfos)
            {
                if (plugin == pluginInfo)
                    continue;

                plugin.IsEnabled = false;
                pinnedFile = string.IsNullOrEmpty(plugin.ContainingDirectory)
                    ? Path.ChangeExtension(plugin.DLLs[0], Pinned)
                    : Path.Join(plugin.ContainingDirectory, Pinned);
                if (File.Exists(pinnedFile))
                    File.Delete(pinnedFile);
            }
        }
        else
        {
            foreach (var plugin in pluginInfos)
            {
                if (plugin != pluginInfo)
                    plugin.IsEnabled = false;

                var pinnedFile = string.IsNullOrEmpty(plugin.ContainingDirectory)
                    ? Path.ChangeExtension(plugin.DLLs[0], Pinned)
                    : Path.Join(plugin.ContainingDirectory, Pinned);
                if (File.Exists(pinnedFile))
                    File.Delete(pinnedFile);
            }
        }

        return pluginInfo;
    }

    #endregion

    #region Types & Exports

    public IEnumerable<Type> GetTypes<T>()
        => _exportedTypes.Where(type => typeof(T).IsAssignableFrom(type));

    public IEnumerable<Type> GetTypes<T>(IPlugin plugin)
        => GetPluginInfo(plugin) is { IsActive: true } pluginInfo
            ? pluginInfo.Types.Where(type => typeof(T).IsAssignableFrom(type))
            : [];

    public T? GetExport<T>(Type type)
        => !typeof(T).IsAssignableFrom(type) ? default : typeof(T).IsValueType ? (T?)Activator.CreateInstance(type) : (T?)ActivatorUtilities.GetServiceOrCreateInstance(Utils.ServiceContainer, type);

    public IEnumerable<T> GetExports<T>()
        => GetTypes<T>()
            .Select(t =>
            {
                try
                {
                    return ActivatorUtilities.GetServiceOrCreateInstance(Utils.ServiceContainer, t);
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
                    return ActivatorUtilities.GetServiceOrCreateInstance(Utils.ServiceContainer, t);
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
        => Utils.ServiceContainer.GetService(type);

    public T? GetService<T>()
        => Utils.ServiceContainer.GetService<T>();

    public object GetRequiredService(Type type)
        => Utils.ServiceContainer.GetRequiredService(type);

    public T GetRequiredService<T>() where T : notnull
        => Utils.ServiceContainer.GetRequiredService<T>();

    #endregion
}
