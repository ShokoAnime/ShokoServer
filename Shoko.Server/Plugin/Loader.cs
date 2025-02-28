﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.Plugin;

public static class Loader
{
    private static readonly List<Type> _exportedTypes = new List<Type>();
    private static readonly List<Type> _pluginTypes = new List<Type>();
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private static IDictionary<Type, IPlugin> Plugins { get; } = new Dictionary<Type, IPlugin>();

    internal static IServiceCollection AddPlugins(this IServiceCollection serviceCollection)
    {
        // add plugin api related things to service collection
        var assemblies = new List<(Assembly, string)>();
        var dirname = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        assemblies.Add((Assembly.GetCallingAssembly(), string.Empty)); //add this to dynamically load as well.

        // Load plugins from the user config dir too.
        var userPluginDir = Path.Combine(Utils.ApplicationPath, "plugins");
        var userPlugins = Directory.Exists(userPluginDir)
            ? Directory.GetFiles(userPluginDir, "*.dll", SearchOption.AllDirectories)
            : Array.Empty<string>();

        // using static reference because we have a pre-init settings handler, which will be updated after init
        var settingsChanged = false;
        var settings = Utils.SettingsProvider.GetSettings();
        foreach (var dll in userPlugins.Concat(Directory.GetFiles(dirname, "plugins/*.dll", SearchOption.AllDirectories)))
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (settings.Plugins.EnabledPlugins.TryGetValue(name, out var isEnabled) && !isEnabled)
                {
                    s_logger.Info($"Found {name}, but it is disabled in the Server Settings. Skipping it.");
                    continue;
                }

                s_logger.Trace($"Checking assembly for IPlugin implementations; {dll}");
                var assembly = Assembly.LoadFrom(dll);
                var pluginTypes = assembly.GetTypes();
                var pluginImpl = pluginTypes
                    .Where(a => a.GetInterfaces().Contains(typeof(IPlugin)))
                    .ToList();
                if (pluginImpl.Count == 0)
                    continue;
                s_logger.Info($"Found assembly with IPlugin implementation ({pluginImpl[0].FullName}); {dll}");

                // TryAdd, because if it made it this far, then it's missing or true.
                if (settings.Plugins.EnabledPlugins.TryAdd(name, true))
                    settingsChanged = true;

                if (!settings.Plugins.Priority.Contains(name))
                {
                    settings.Plugins.Priority.Add(name);
                    settingsChanged = true;
                }

                assemblies.Add((assembly, name));
            }
            catch (Exception ex)
            {
                s_logger.Warn(ex, "Failed to load plugin {Name}", Path.GetFileNameWithoutExtension(dll));
            }
        }

        if (settingsChanged)
            Utils.SettingsProvider.SaveSettings();

        var orderedAssemblies = assemblies
            .OrderBy(a => settings.Plugins.Priority.IndexOf(a.Item2))
            .Select(a => a.Item1)
            .ToList();
        LoadPlugins(orderedAssemblies, serviceCollection);

        return serviceCollection;
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
            var xml = Path.Combine(Path.GetDirectoryName(location),
                $"{Path.GetFileNameWithoutExtension(location)}.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml, true); //Include the XML comments if it exists.
            }
        }

        return options;
    }

    private static void LoadPlugins(IEnumerable<Assembly> assemblies, IServiceCollection serviceCollection)
    {
        s_logger.Trace("Scanning for IPlugin and IPluginServiceRegistration implementations");
        foreach (var assembly in assemblies)
        {
            var pluginTypes = assembly.GetTypes();
            var pluginImpl = pluginTypes
                .Where(a => a.GetInterfaces().Contains(typeof(IPlugin)))
                .ToList();
            if (pluginImpl.Count == 0)
                continue;

            if (pluginImpl.Count > 1)
            {
                s_logger.Warn(
                    "Multiple implementations of IPlugin found in {0}. Using the first implementation: {1}",
                    assembly.FullName,
                    pluginImpl[0].Name
                );
            }

            var pluginType = pluginImpl[0];
            s_logger.Trace("Found IPlugin implementation: {0}", pluginType.FullName);
            _pluginTypes.Add(pluginType);

            var exportedTypeList = assembly.GetExportedTypes();
            foreach (var type in exportedTypeList)
                if (type.IsClass && !type.IsAbstract && !type.IsInterface && !type.IsGenericType)
                    _exportedTypes.Add(type);

            // Compat. for plugins targeting <4.2.0-beta2 using the previously undocumented (except in source code) ConfigureServices method.
            if (pluginType.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is { } mtd)
            {
                s_logger.Trace("Registering plugin service: {0}", pluginType.FullName);
                mtd.Invoke(null, [serviceCollection]);
                continue;
            }

            var registrationImpl = pluginTypes
                .Where(a => a.GetInterfaces().Contains(typeof(IPluginServiceRegistration)))
                .ToList();
            if (registrationImpl.Count == 0)
                continue;

            if (registrationImpl.Count > 1)
            {
                s_logger.Warn(
                    "Multiple IPluginServiceRegistrations found in {0}. Using the first implementation: {1}",
                    assembly.FullName,
                    registrationImpl[0].Name
                );
            }

            var registrationType = registrationImpl[0];
            s_logger.Trace("Registering plugin service: {0}", registrationType.FullName);

            try
            {
                var instance = Activator.CreateInstance(registrationType) as IPluginServiceRegistration;
                instance?.RegisterServices(serviceCollection, ApplicationPaths.Instance);
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Error registering plugin services from {0}.", registrationType.Assembly.FullName);
                continue;
            }
        }
    }

    internal static void InitPlugins(IServiceProvider provider)
    {
        s_logger.Info("Loading {0} plugins", _pluginTypes.Count);

        foreach (var pluginType in _pluginTypes)
        {
            var plugin = (IPlugin)ActivatorUtilities.CreateInstance(provider, pluginType);
            Plugins.Add(pluginType, plugin);
            LoadSettings(pluginType, plugin);
            s_logger.Info($"Loaded: {plugin.Name} ({pluginType.FullName})");
            plugin.Load();
        }

        // Used to store the updated priorities for the providers in the settings file.
        var videoReleaseService = provider.GetRequiredService<IVideoReleaseService>();
        videoReleaseService.AddProviders(GetExports<IReleaseInfoProvider>(provider));
        videoReleaseService.UpdateProviders();

        var videoHashingService = provider.GetRequiredService<IVideoHashingService>();
        videoHashingService.AddProviders(GetExports<IHashProvider>(provider));
        videoHashingService.UpdateProviders();
    }

    public static List<T> GetExports<T>()
        => GetExports<T>(Utils.ServiceContainer);

    private static List<T> GetExports<T>(IServiceProvider serviceProvider)
        => _exportedTypes
            .Where(type => typeof(T).IsAssignableFrom(type))
            .Select(t => ActivatorUtilities.CreateInstance(serviceProvider, t))
            .WhereNotNull()
            .Cast<T>()
            .ToList();

    private static void LoadSettings(Type type, IPlugin plugin)
    {
        var (name, t) = type.Assembly.GetTypes()
            .Where(p => p.IsClass && typeof(IPluginSettings).IsAssignableFrom(p))
            .DistinctBy(a => a.Assembly.GetName().Name)
            .Select(a => (a.Assembly.GetName().Name + ".json", a)).FirstOrDefault();
        if (string.IsNullOrEmpty(name) || name == ".json") return;

        try
        {
            var serverSettings = Utils.SettingsProvider.GetSettings();
            if (serverSettings.Plugins.EnabledPlugins.ContainsKey(name) && !serverSettings.Plugins.EnabledPlugins[name])
                return;

            var settingsPath = Path.Combine(Utils.ApplicationPath, "plugins", name);
            var obj = !File.Exists(settingsPath)
                ? Activator.CreateInstance(t)
                : SettingsProvider.Deserialize(t, File.ReadAllText(settingsPath));
            var settings = (IPluginSettings)obj;

            plugin.OnSettingsLoaded(settings);
        }
        catch (Exception e)
        {
            s_logger.Error(e, $"Unable to initialize Settings for {name}");
        }
    }

    public static void SaveSettings(IPluginSettings settings)
    {
        var name = settings.GetType().Assembly.GetName().Name + ".json";
        if (string.IsNullOrEmpty(name) || name == ".json") return;

        try
        {
            var settingsPath = Path.Combine(Utils.ApplicationPath, "plugins", name);
            Directory.CreateDirectory(Path.Combine(Utils.ApplicationPath, "plugins"));
            var json = SettingsProvider.Serialize(settings);
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception e)
        {
            s_logger.Error(e, $"Unable to Save Settings for {name}");
        }
    }
}
