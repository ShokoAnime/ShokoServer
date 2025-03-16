using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Services;
using Shoko.Server.Renamer;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Swashbuckle.AspNetCore.SwaggerGen;

#nullable enable
namespace Shoko.Server.Plugin;

public static class Loader
{
    private static readonly List<Type> _exportedTypes = [];
    private static readonly List<Type> _pluginTypes = [];
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    internal static Dictionary<Type, IPlugin> Plugins { get; } = [];

    /// <summary>
    /// Add plugin related services to the service collection.
    /// </summary>
    /// <param name="serviceCollection">Service Collection.</param>
    /// <param name="settingsProvider">Settings provider.</param>
    /// <returns>The <paramref name="serviceCollection"/>.</returns>
    internal static IServiceCollection AddPlugins(this IServiceCollection serviceCollection, ISettingsProvider settingsProvider)
    {
        // Load plugins from the system directory.
        var systemPluginDir = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "plugins");
        var systemPlugins = Directory.Exists(systemPluginDir)
            ? Directory.GetFiles(systemPluginDir, "*.dll", SearchOption.AllDirectories)
            : [];

        // Load plugins from the user config directory.
        var userPluginDir = Path.Join(Utils.ApplicationPath, "plugins");
        var userPlugins = Directory.Exists(userPluginDir)
            ? Directory.GetFiles(userPluginDir, "*.dll", SearchOption.AllDirectories)
            : [];

        // Add the core plugin to register it's plugin providers.
        var assemblies = new List<(Assembly PluginAssembly, Type PluginType, Type? ServiceRegistrationType, string DllName)>
        {
            (Assembly.GetCallingAssembly(), typeof(CorePlugin), null, string.Empty),
        };

        s_logger.Trace("Scanning {0} DLLs for IPlugin and IPluginServiceRegistration implementations", userPlugins.Length + systemPlugins.Length);
        var settingsChanged = false;
        var settings = settingsProvider.GetSettings();
        foreach (var dllPath in userPlugins.Concat(systemPlugins))
        {
            var name = Path.GetFileNameWithoutExtension(dllPath);
            try
            {
                if (settings.Plugins.EnabledPlugins.TryGetValue(name, out var isEnabled) && !isEnabled)
                {
                    s_logger.Info($"Found {name}, but it is disabled in the Server Settings. Skipping it.");
                    continue;
                }
                var assembly = Assembly.LoadFrom(dllPath);
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName) || !string.Equals(assemblyName, name, StringComparison.Ordinal))
                {
                    s_logger.Info("Skipping {0} because the loaded assembly does not have the same name as the file.", dllPath);
                    continue;
                }

                var version = assembly.GetName().Version ?? new(0, 0, 0, 0);
                var pluginTypes = assembly.GetExportedTypes();
                var pluginImpl = pluginTypes
                    .Where(a => a.GetInterfaces().Contains(typeof(IPlugin)))
                    .ToList();
                if (pluginImpl.Count == 0)
                    continue;

                if (pluginImpl.Count > 1)
                {
                    s_logger.Warn(
                        "Multiple implementations of IPlugin found in {0}. Using the first implementation: {1}",
                        name,
                        pluginImpl[0].Name
                    );
                    continue;
                }

                var registrationImpl = pluginTypes
                    .Where(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration)))
                    .ToList();
                if (registrationImpl.Count > 1)
                {
                    s_logger.Warn(
                        "Multiple IPluginServiceRegistrations found in {0}. Using the first implementation: {1}",
                        name,
                        registrationImpl[0].Name
                    );
                }

                if (registrationImpl.Count > 0)
                    s_logger.Info("Found IPlugin & IPluginServiceRegistration implementations. ({0}, v{1})", name, version);
                else
                    s_logger.Info("Found IPlugin implementation. ({0}, v{1})", name, version);

                // TryAdd, because if it made it this far, then it's missing or true.
                if (settings.Plugins.EnabledPlugins.TryAdd(name, true))
                    settingsChanged = true;

                if (!settings.Plugins.Priority.Contains(name))
                {
                    settings.Plugins.Priority.Add(name);
                    settingsChanged = true;
                }

                assemblies.Add((assembly, pluginImpl[0], registrationImpl.Count > 0 ? registrationImpl[0] : null, name));
            }
            catch (Exception ex)
            {
                s_logger.Warn(ex, "Failed to check assembly {Name} for IPlugin and IPluginServiceRegistration implementations; {1}", name, dllPath);
            }
        }

        if (settingsChanged)
            settingsProvider.SaveSettings();

        // Register the plugins in order of priority & then register their services.
        if (assemblies.Any(a => a.ServiceRegistrationType is not null))
            s_logger.Trace("Registering services for {0} plugins.", assemblies.Count(a => a.ServiceRegistrationType is not null));

        foreach (var (assembly, pluginType, registrationType, dllName) in assemblies.OrderBy(a => settings.Plugins.Priority.IndexOf(a.DllName)))
        {
            var version = assembly.GetName().Version ?? new(0, 0, 0, 0);
            _pluginTypes.Add(pluginType);
            _exportedTypes.AddRange(
                assembly.GetExportedTypes()
                    .Where(type => type.IsClass && !type.IsAbstract && !type.IsInterface && !type.IsGenericType)
            );

            if (registrationType is not null)
            {
                s_logger.Trace("Registering plugin services. ({0}, v{1})", dllName, version);
                var instance = (IPluginServiceRegistration)Activator.CreateInstance(registrationType)!;
                instance.RegisterServices(serviceCollection, ApplicationPaths.Instance);
            }
            // Compat. for plugins targeting <4.2.0-beta2 using the previously undocumented (except in source code) ConfigureServices method.
            else if (pluginType.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is { } mtd)
            {
                s_logger.Trace("Registering plugin services. ({0}, v{1})", dllName, version);
                mtd.Invoke(null, [serviceCollection]);
            }
        }

        return serviceCollection;
    }

    internal static void InitPlugins(IServiceProvider provider)
    {
        s_logger.Info("Initializing {0} plugins.", _pluginTypes.Count);
        foreach (var pluginType in _pluginTypes)
        {
            var assemblyInfo = pluginType.Assembly.GetName();
            var plugin = (IPlugin)ActivatorUtilities.CreateInstance(provider, pluginType);
            Plugins.Add(pluginType, plugin);
            s_logger.Info($"Initialized plugin \"{plugin.Name}\". ({assemblyInfo.Name}, v{assemblyInfo.Version}).");
        }

        var pluginManager = provider.GetRequiredService<IPluginManager>();
        pluginManager.AddParts(Plugins.Values);

        var configurationService = provider.GetRequiredService<IConfigurationService>();
        configurationService.AddParts(GetTypes<IConfiguration>(), GetExports<IConfigurationDefinition>(provider));

        // Used to store the updated priorities for the providers in the settings file.
        var videoReleaseService = provider.GetRequiredService<IVideoReleaseService>();
        videoReleaseService.AddParts(GetExports<IReleaseInfoProvider>(provider));

        var videoHashingService = provider.GetRequiredService<IVideoHashingService>();
        videoHashingService.AddParts(GetExports<IHashProvider>(provider));

        var renameFileService = provider.GetRequiredService<RenameFileService>();
        renameFileService.LoadRenamers(_exportedTypes);

        s_logger.Info("Loading {0} plugins.", _pluginTypes.Count);
        foreach (var (pluginType, plugin) in Plugins)
        {
            var assemblyInfo = pluginType.Assembly.GetName();
            plugin.Load();
            s_logger.Info($"Loaded plugin \"{plugin.Name}\". ({assemblyInfo.Name}, v{assemblyInfo.Version})");
        }
    }

    public static IMvcBuilder AddPluginControllers(this IMvcBuilder mvc)
    {
        foreach (var type in Plugins.Keys)
        {
            var assembly = type.Assembly;
            if (assembly == Assembly.GetCallingAssembly())
            {
                continue; //Skip the current assembly, this is implicitly added by ASP.
            }

            mvc.AddApplicationPart(assembly);
        }

        return mvc;
    }

    public static SwaggerGenOptions AddPlugins(this SwaggerGenOptions options)
    {
        foreach (var type in Plugins.Keys)
        {
            var assembly = type.Assembly;
            var location = assembly.Location;
            var xml = Path.ChangeExtension(location, "xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml, true); //Include the XML comments if it exists.
            }
        }

        return options;
    }

    public static IPlugin? GetFromType(Type pluginType)
        => Plugins.GetValueOrDefault(pluginType);

    public static IEnumerable<Type> GetTypes<T>()
        => _exportedTypes.Where(type => typeof(T).IsAssignableFrom(type));

    public static IEnumerable<Type> GetTypes<T>(Assembly assembly)
        => assembly.GetTypes().Where(type => typeof(T).IsAssignableFrom(type));

    public static IEnumerable<T> GetExports<T>()
        => GetExports<T>(Utils.ServiceContainer);

    public static IEnumerable<T> GetExports<T>(Assembly assembly)
        => GetTypes<T>(assembly)
            .Select(t => ActivatorUtilities.CreateInstance(Utils.ServiceContainer, t))
            .WhereNotNull()
            .Cast<T>();

    private static IEnumerable<T> GetExports<T>(IServiceProvider serviceProvider)
        => GetTypes<T>()
            .Select(t => ActivatorUtilities.CreateInstance(serviceProvider, t))
            .WhereNotNull()
            .Cast<T>();
}
