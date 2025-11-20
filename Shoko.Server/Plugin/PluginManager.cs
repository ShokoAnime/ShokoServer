using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Relocation;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Plugin;

public partial class PluginManager(ILogger<PluginManager> logger, IApplicationPaths applicationPaths) : IPluginManager
{
    private readonly List<Type> _exportedTypes = [];

    private readonly List<PluginInfo> _pluginTypes = [];

    private readonly Guid _coreID = typeof(CorePlugin).FullName!.ToUuidV5();

    private readonly Version _invalidVersion = new(0, 0, 0, 0);

    #region Setup

    private sealed class BasicPluginInfo
    {
        public required Guid ID { get; init; }

        public required string DllName { get; init; }

        public required string Name { get; init; }

        public required string Description { get; init; }

        public required Version Version { get; init; }

        public required int Priority { get; init; }

        public required bool IsPinned { get; init; }

        public required bool IsEnabled { get; init; }

        public required bool HasServices { get; init; }

        public required bool CanUninstall { get; init; }

        public required string? ContainingDirectory { get; init; }

        public required string[] DLLs { get; init; }
    }

    private class IsolatedLoadContext() : AssemblyLoadContext(isCollectible: true) { }

    public void RegisterPlugins(IServiceCollection serviceCollection)
    {
        if (_pluginTypes.Count > 0)
            throw new InvalidOperationException("Plugins have already been registered.");

        // Add the core plugin to register it's plugin providers.
        var basicPlugins = new List<BasicPluginInfo>()
        {
            new()
            {
                ID = _coreID,
                Name = "Shoko Core",
                DllName = Path.GetFileNameWithoutExtension(Assembly.GetCallingAssembly().Location!),
                Description = string.Empty,
                Version = Assembly.GetCallingAssembly().GetName().Version ?? new(1, 0, 0),
                IsPinned = true,
                IsEnabled = true,
                HasServices = false,
                Priority = -1,
                CanUninstall = false,
                ContainingDirectory = null,
                DLLs = [Assembly.GetCallingAssembly().Location!],
            },
        };

        var directories = GetPluginDirectories().ToArray();
        logger.LogTrace("Scanning {Count} directories for IPlugin and IPluginServiceRegistration implementations", directories.Length);
        var settingsChanged = false;
        var settings = Utils.SettingsProvider.GetSettings();
        foreach (var (dirPath, dlls, isSystem) in directories)
            if (LoadBasicPluginInfo(dirPath, dlls, isSystem, settings, ref settingsChanged) is { } basicPluginInfo)
                basicPlugins.Add(basicPluginInfo);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (settingsChanged)
            Utils.SettingsProvider.SaveSettings();

        // Register the plugins in order of priority & then register their services.
        if (basicPlugins.Any(a => a.HasServices))
            logger.LogTrace("Registering services for {Count} plugins.", basicPlugins.Count(a => a.HasServices));

        foreach (var grouping in basicPlugins.OrderBy(a => a.Priority).GroupBy(a => a.ID))
        {
            var orderedInfo = grouping
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.Version)
                .ThenByDescending(a => a.ContainingDirectory is not null)
                .ThenBy(a => a.ContainingDirectory)
                .ThenBy(a => a.DLLs[0])
                .ToList();
            var enabled = true;
            foreach (var basicPluginInfo in orderedInfo)
            {
                if (!basicPluginInfo.IsEnabled || !enabled)
                {
                    _pluginTypes.Add(new()
                    {
                        ID = basicPluginInfo.ID,
                        Version = basicPluginInfo.Version,
                        Name = basicPluginInfo.Name,
                        Description = basicPluginInfo.Description,
                        LoadOrder = _pluginTypes.Count,
                        IsInstalled = true,
                        IsEnabled = false,
                        IsActive = false,
                        CanUninstall = basicPluginInfo.CanUninstall,
                        Plugin = null,
                        PluginType = null,
                        ContainingDirectory = basicPluginInfo.ContainingDirectory,
                        DLLs = basicPluginInfo.DLLs,
                        Types = [],
                    });
                    continue;
                }

                enabled = false;
                var mainDllPath = basicPluginInfo.DLLs[0];
                var assembly = mainDllPath is null ? Assembly.GetCallingAssembly() : Assembly.LoadFrom(mainDllPath);
                var types = assembly.GetExportedTypes()
                    .Where(type => type.IsClass && !type.IsAbstract && !type.IsInterface && !type.IsGenericType)
                    .ToArray();
                _pluginTypes.Add(new()
                {
                    ID = basicPluginInfo.ID,
                    Version = basicPluginInfo.Version,
                    Name = basicPluginInfo.Name,
                    Description = basicPluginInfo.Description,
                    LoadOrder = _pluginTypes.Count,
                    IsInstalled = true,
                    IsEnabled = true,
                    IsActive = false,
                    CanUninstall = basicPluginInfo.CanUninstall,
                    Plugin = null,
                    PluginType = types.Where(a => a.GetInterfaces().Contains(typeof(IPlugin))).First(),
                    ContainingDirectory = basicPluginInfo.ContainingDirectory,
                    DLLs = basicPluginInfo.DLLs,
                    Types = types,
                });
                var registrationType = basicPluginInfo.HasServices ? assembly.GetExportedTypes().First(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration))) : null;
                if (registrationType is not null)
                {
                    logger.LogTrace("Registering plugin services. ({DllName}, v{Version})", basicPluginInfo.DllName, basicPluginInfo.Version);
                    var instance = (IPluginServiceRegistration)Activator.CreateInstance(registrationType)!;
                    instance.RegisterServices(serviceCollection, applicationPaths);
                }
            }
        }
    }

    public void InitPlugins()
    {
        if (_exportedTypes.Count > 0)
            throw new InvalidOperationException("Plugins have already been initialized.");

        logger.LogInformation("Initializing {Count} plugins. ({Disabled} disabled)", _pluginTypes.Count(a => a.IsEnabled), _pluginTypes.Count(a => !a.IsEnabled));
        foreach (var basicPluginInfo in _pluginTypes.ToArray())
        {
            var dllName = Path.GetFileNameWithoutExtension(basicPluginInfo.DLLs[0]);
            if (!basicPluginInfo.IsEnabled)
            {
                logger.LogInformation("Skipping disabled plugin \"{Name}\". ({DllName}, v{Version})", basicPluginInfo.Name, dllName, basicPluginInfo.Version);
                continue;
            }

            var pluginType = basicPluginInfo.PluginType!;
            var plugin = (IPlugin)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, pluginType);
            _pluginTypes[basicPluginInfo.LoadOrder] = new()
            {
                ID = plugin.ID,
                Name = plugin.Name,
                Description = plugin.Description?.CleanDescription() ?? string.Empty,
                Version = basicPluginInfo.Version,
                LoadOrder = basicPluginInfo.LoadOrder,
                IsInstalled = true,
                IsEnabled = true,
                IsActive = true,
                CanUninstall = basicPluginInfo.CanUninstall,
                Plugin = plugin,
                PluginType = pluginType,
                ContainingDirectory = basicPluginInfo.ContainingDirectory,
                DLLs = basicPluginInfo.DLLs,
                Types = basicPluginInfo.Types,
            };
            _exportedTypes.AddRange(basicPluginInfo.Types);

            logger.LogInformation("Initialized plugin \"{Name}\". ({DllName}, v{Version})", plugin.Name, dllName, basicPluginInfo.Version);
        }

        var configurationService = Utils.ServiceContainer.GetRequiredService<IConfigurationService>();
        configurationService.AddParts(GetTypes<IConfiguration>(), GetTypes<IConfigurationDefinition>());

        // Used to store the updated priorities for the providers in the settings file.
        var videoReleaseService = Utils.ServiceContainer.GetRequiredService<IVideoReleaseService>();
        videoReleaseService.AddParts(GetExports<IReleaseInfoProvider>());

        var videoHashingService = Utils.ServiceContainer.GetRequiredService<IVideoHashingService>();
        videoHashingService.AddParts(GetExports<IHashProvider>());

        var relocationService = Utils.ServiceContainer.GetRequiredService<IRelocationService>();
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
                var removeFile = Path.ChangeExtension(filePath, ".remove");
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin DLL file marked for removal: {Path}", filePath);
                    File.Delete(removeFile);
                    File.Delete(filePath);
                    continue;
                }

                yield return (null, [filePath], true);
            }

            foreach (var directoryPath in Directory.GetDirectories(systemPluginDir, "*", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.Join(directoryPath, ".remove");
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin directory marked for removal: {Path}", directoryPath);
                    File.Delete(removeFile);
                    Directory.Delete(directoryPath, true);
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
                var removeFile = Path.ChangeExtension(filePath, ".remove");
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin DLL file marked for removal: {Path}", filePath);
                    File.Delete(removeFile);
                    File.Delete(filePath);
                    continue;
                }

                yield return (null, [filePath], false);
            }

            foreach (var directoryPath in Directory.GetDirectories(userPluginDir, "*", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
            {
                var removeFile = Path.Join(directoryPath, ".remove");
                if (File.Exists(removeFile))
                {
                    logger.LogInformation("Removing plugin directory marked for removal: {Path}", directoryPath);
                    File.Delete(removeFile);
                    Directory.Delete(directoryPath, true);
                    continue;
                }

                yield return (directoryPath, Directory.GetFiles(directoryPath, "*.dll", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }), false);
            }
        }
    }

    private BasicPluginInfo? LoadBasicPluginInfo(string? containingDirectory, string[] dlls)
    {
        var settingsChanged = false;
        var settings = Utils.SettingsProvider.GetSettings();
        var basicPluginInfo = LoadBasicPluginInfo(containingDirectory, dlls, false, settings, ref settingsChanged);
        if (settingsChanged)
            Utils.SettingsProvider.SaveSettings();

        if (basicPluginInfo is not null)
            logger.LogInformation("Loaded inactive plugin \"{Name}\". ({DllName}, v{Version})", basicPluginInfo.Name, Path.GetFileNameWithoutExtension(basicPluginInfo.DLLs[0]), basicPluginInfo.Version);
        return basicPluginInfo;
    }

    private BasicPluginInfo? LoadBasicPluginInfo(string? dirPath, string[] dlls, bool isSystem, IServerSettings settings, ref bool settingsChanged)
    {
        var alc = new IsolatedLoadContext();
        try
        {
            foreach (var dllPath in dlls)
            {
                var name = Path.GetFileNameWithoutExtension(dllPath);
                try
                {
                    var assembly = alc.LoadFromAssemblyPath(dllPath);
                    var assemblyName = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(assemblyName) || !string.Equals(assemblyName, name, StringComparison.Ordinal))
                    {
                        logger.LogInformation("Skipping {DllName} because the loaded assembly does not have the same name as the file.", dllPath);
                        continue;
                    }

                    var version = assembly.GetName().Version is { } assVer ? new Version(assVer.Major, assVer.Minor, assVer.Build, assVer.Revision) : new(0, 0, 0, 0);
                    if (version <= _invalidVersion)
                    {
                        logger.LogInformation("Skipping {DllName} because the loaded assembly has a version below or equal to v0.0.0.0.", dllPath);
                        continue;
                    }

                    var pluginTypes = assembly.GetExportedTypes();
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
                        logger.LogInformation("Found IPlugin & IPluginServiceRegistration implementations. ({DllName}, v{Version})", name, version);
                    else
                        logger.LogInformation("Found IPlugin implementation. ({DllName}, v{Version})", name, version);

                    // TryAdd, because if it made it this far, then it's missing or true.
                    if (settings.Plugins.EnabledPlugins.TryAdd(name, true))
                        settingsChanged = true;

                    if (!settings.Plugins.Priority.Contains(name))
                    {
                        settings.Plugins.Priority.Add(name);
                        settingsChanged = true;
                    }


                    var instance = (IPlugin)Activator.CreateInstance(pluginImpl[0])!;
                    if (instance.ID == _coreID)
                    {
                        logger.LogInformation("Skipping {DllName} because it has the same ID as the core plugin.", dllPath);
                        continue;
                    }
                    return new()
                    {
                        ID = instance.ID,
                        DllName = name,
                        Name = instance.Name,
                        Description = instance.Description?.CleanDescription() ?? string.Empty,
                        Version = version,
                        IsPinned = string.IsNullOrEmpty(dirPath)
                            ? File.Exists(Path.ChangeExtension(dllPath, ".pinned"))
                            : File.Exists(Path.Join(dirPath, ".pinned")),
                        IsEnabled = settings.Plugins.EnabledPlugins[name],
                        HasServices = registrationImpl.Count > 0,
                        ContainingDirectory = dirPath,
                        Priority = settings.Plugins.Priority.IndexOf(name),
                        CanUninstall = !isSystem,
                        DLLs = [dllPath, .. dlls.Except([dllPath])],
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

    #endregion

    #region Plugin Info

    public IReadOnlyList<PluginInfo> GetPluginInfos()
        => _pluginTypes;

    public IReadOnlyList<PluginInfo> GetPluginInfos(Guid pluginId)
        => _pluginTypes.Where(p => p.ID == pluginId).ToList();

    public PluginInfo? GetPluginInfo(Guid pluginId, Version? pluginVersion = null)
        => pluginVersion is not null
            ? _pluginTypes.FirstOrDefault(p => p.ID == pluginId && p.Version == pluginVersion)
            : _pluginTypes.FirstOrDefault(p => p.ID == pluginId && p.IsActive) ?? _pluginTypes.Where(p => p.ID == pluginId).OrderByDescending(p => p.Version).FirstOrDefault();

    public PluginInfo? GetPluginInfo(IPlugin plugin)
        => _pluginTypes.FirstOrDefault(p => p.Plugin is not null && ReferenceEquals(plugin, p.Plugin));

    public PluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin
        => _pluginTypes.FirstOrDefault(p => typeof(TPlugin) == p.PluginType);

    public PluginInfo? GetPluginInfo(Type type)
        => _pluginTypes.FirstOrDefault(p => type == p.PluginType);

    public PluginInfo? GetPluginInfo(Assembly assembly)
        => assembly.GetTypes().Where(type => typeof(IPlugin).IsAssignableFrom(type)).FirstOrDefault() is { } pluginType
            ? GetPluginInfo(pluginType)
            : null;

    #endregion

    #region Plugin Management

    public PluginInfo? LoadFromPath(string path)
    {
        var userPluginDir = applicationPaths.PluginsPath;
        if (path.StartsWith("%PluginsPath%"))
            path = path.Replace("%PluginsPath%", userPluginDir);

        if (Path.IsPathFullyQualified(path))
        {
            if (!path.StartsWith(userPluginDir + Path.DirectorySeparatorChar))
                return null;

            return LoadFromPathInternal(path);
        }

        var fullPath = Path.Combine(userPluginDir, path);
        if (!fullPath.StartsWith(userPluginDir + Path.DirectorySeparatorChar))
            return null;

        return LoadFromPathInternal(fullPath);
    }

    public PluginInfo EnablePlugin(PluginInfo pluginInfo)
        => TogglePlugin(pluginInfo, true);

    public PluginInfo DisablePlugin(PluginInfo pluginInfo)
        => TogglePlugin(pluginInfo, false);

    public PluginInfo UninstallPlugin(PluginInfo pluginInfo, bool purgeConfiguration = true)
    {
        if (!pluginInfo.CanUninstall || !pluginInfo.IsInstalled)
            return pluginInfo;

        // Mark the plugin for removal upon next startup.
        if (!string.IsNullOrEmpty(pluginInfo.ContainingDirectory))
        {
            if (Directory.Exists(pluginInfo.ContainingDirectory))
            {
                var removalFile = Path.Join(pluginInfo.ContainingDirectory, ".remove");
                var pinnedFile = Path.Join(pluginInfo.ContainingDirectory, ".pinned");
                if (!File.Exists(removalFile))
                    File.WriteAllText(removalFile, string.Empty);
                if (File.Exists(pinnedFile))
                    File.Delete(pinnedFile);
            }
        }
        else if (pluginInfo.DLLs.Count is 1)
        {
            var removalFile = Path.ChangeExtension(pluginInfo.DLLs[0], ".remove");
            var pinnedFile = Path.ChangeExtension(pluginInfo.DLLs[0], ".pinned");
            if (File.Exists(pluginInfo.DLLs[0]) && !File.Exists(removalFile))
                File.WriteAllText(removalFile, string.Empty);
            if (File.Exists(pinnedFile))
                File.Delete(pinnedFile);
        }

        // Disable it and marked it as not installed.
        pluginInfo.IsInstalled = false;
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

        return pluginInfo;
    }

    private PluginInfo? LoadFromPathInternal(string path)
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

    private PluginInfo? LoadFromDirectoryOrDLL(string? containingDirectory, string[] dlls)
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

        if (LoadBasicPluginInfo(containingDirectory, dlls) is not { } basicPluginInfo)
            return null;

        var pluginInfo = new PluginInfo()
        {
            ID = basicPluginInfo.ID,
            Version = basicPluginInfo.Version,
            Name = basicPluginInfo.Name,
            Description = basicPluginInfo.Description,
            LoadOrder = _pluginTypes.Count,
            IsInstalled = true,
            IsEnabled = basicPluginInfo.IsEnabled,
            IsActive = false,
            CanUninstall = basicPluginInfo.CanUninstall,
            Plugin = null,
            PluginType = null,
            ContainingDirectory = basicPluginInfo.ContainingDirectory,
            DLLs = basicPluginInfo.DLLs,
            Types = [],
        };
        _pluginTypes.Add(pluginInfo);
        return pluginInfo;
    }

    private PluginInfo TogglePlugin(PluginInfo pluginInfo, bool enabled)
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
                ? Path.ChangeExtension(pluginInfo.DLLs[0], ".pinned")
                : Path.Join(pluginInfo.ContainingDirectory, ".pinned");
            if (!File.Exists(pinnedFile))
                File.WriteAllText(pinnedFile, string.Empty);
            foreach (var plugin in pluginInfos)
            {
                if (plugin == pluginInfo)
                    continue;

                plugin.IsEnabled = false;
                pinnedFile = string.IsNullOrEmpty(plugin.ContainingDirectory)
                    ? Path.ChangeExtension(plugin.DLLs[0], ".pinned")
                    : Path.Join(plugin.ContainingDirectory, ".pinned");
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
                    ? Path.ChangeExtension(plugin.DLLs[0], ".pinned")
                    : Path.Join(plugin.ContainingDirectory, ".pinned");
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
            .Select(t => ActivatorUtilities.GetServiceOrCreateInstance(Utils.ServiceContainer, t))
            .WhereNotNull()
            .Cast<T>();

    public IEnumerable<T> GetExports<T>(IPlugin plugin)
        => GetTypes<T>(plugin)
            .Select(t => ActivatorUtilities.GetServiceOrCreateInstance(Utils.ServiceContainer, t))
            .WhereNotNull()
            .Cast<T>();

    #endregion
}
