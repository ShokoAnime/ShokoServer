using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using ISettingsProvider = Shoko.Plugin.Abstractions.ISettingsProvider;

namespace Shoko.Server.Plugin;

public class Loader : ISettingsProvider
{
    private readonly IList<Type> _pluginTypes = new List<Type>();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static Loader _instance;
    public static Loader Instance { get => _instance ??= new(); }
    public IDictionary<Type, IPlugin> Plugins { get; } = new Dictionary<Type, IPlugin>();
    public static Settings.ISettingsProvider ISettingsProvider;

    internal void Load(IServiceCollection serviceCollection)
    {
        // add plugin api related things to service collection
        serviceCollection.AddSingleton<IShokoEventHandler>(ShokoEventHandler.Instance);

        var assemblies = new List<Assembly>();
        var assembly = Assembly.GetExecutingAssembly();
        var uri = new UriBuilder(assembly.GetName().CodeBase);
        var dirname = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
        // if (dirname == null) return;
        assemblies.Add(Assembly.GetCallingAssembly()); //add this to dynamically load as well.

        //Load plugins from the user config dir too.
        var userPluginDir = Path.Combine(Utils.ApplicationPath, "plugins");
        var userPlugins = Directory.Exists(userPluginDir)
            ? Directory.GetFiles(userPluginDir, "*.dll", SearchOption.AllDirectories)
            : Array.Empty<string>();

        var settings = ISettingsProvider.GetSettings();
        foreach (var dll in userPlugins.Concat(Directory.GetFiles(dirname, "plugins/*.dll", SearchOption.AllDirectories)))
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (settings.Plugins.EnabledPlugins.ContainsKey(name) && !settings.Plugins.EnabledPlugins[name])
                {
                    _logger.Info($"Found {name}, but it is disabled in the Server Settings. Skipping it.");
                    continue;
                }

                _logger.Debug($"Trying to load {dll}");
                assemblies.Add(Assembly.LoadFrom(dll));
                // TryAdd, because if it made it this far, then it's missing or true.
                settings.Plugins.EnabledPlugins.TryAdd(name, true);
                if (!settings.Plugins.Priority.Contains(name)) settings.Plugins.Priority.Add(name);
            }
            catch (FileLoadException)
            {
                _logger.Debug("BadImageFormatException");
            }
            catch (BadImageFormatException)
            {
                _logger.Debug("BadImageFormatException");
            }
        }

        RenameFileHelper.FindRenamers(assemblies);
        LoadPlugins(assemblies, serviceCollection);
    }

    private void LoadPlugins(IEnumerable<Assembly> assemblies, IServiceCollection serviceCollection)
    {
        var implementations = assemblies.SelectMany(a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch (Exception e)
            {
                _logger.Debug(e);
                return new Type[0];
            }
        }).Where(a => a.GetInterfaces().Contains(typeof(IPlugin)));

        foreach (var implementation in implementations)
        {
            var mtd = implementation.GetMethod("ConfigureServices",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (mtd != null)
            {
                mtd.Invoke(null, new object[] { serviceCollection });
            }

            _pluginTypes.Add(implementation);
        }
    }

    internal void InitPlugins(IServiceProvider provider)
    {
        _logger.Info("Loading {0} plugins", _pluginTypes.Count);

        foreach (var pluginType in _pluginTypes)
        {
            var plugin = (IPlugin)ActivatorUtilities.CreateInstance(provider, pluginType);
            Plugins.Add(pluginType, plugin);
            LoadSettings(pluginType, plugin);
            _logger.Info($"Loaded: {plugin.Name}");
            plugin.Load();
        }

        // When we initialized the plugins, we made entries for the Enabled State of Plugins
        ISettingsProvider.SaveSettings();
    }

    private void LoadSettings(Type type, IPlugin plugin)
    {
        var (name, t) = type.Assembly.GetTypes()
            .Where(p => p.IsClass && typeof(IPluginSettings).IsAssignableFrom(p))
            .DistinctBy(a => a.GetAssemblyName())
            .Select(a => (a.GetAssemblyName() + ".json", a)).FirstOrDefault();
        if (string.IsNullOrEmpty(name) || name == ".json") return;

        try
        {
            var serverSettings = ISettingsProvider.GetSettings();
            if (serverSettings.Plugins.EnabledPlugins.ContainsKey(name) && !serverSettings.Plugins.EnabledPlugins[name])
                return;

            var settingsPath = Path.Combine(Utils.ApplicationPath, "Plugins", name);
            var obj = !File.Exists(settingsPath)
                ? Activator.CreateInstance(t)
                : SettingsProvider.Deserialize(t, File.ReadAllText(settingsPath));
            // Plugins.Settings will be empty, since it's ignored by the serializer
            var settings = (IPluginSettings)obj;
            serverSettings.Plugins.Settings.Add(settings);

            plugin.OnSettingsLoaded(settings);
        }
        catch (Exception e)
        {
            _logger.Error(e, $"Unable to initialize Settings for {name}");
        }
    }

    public void SaveSettings(IPluginSettings settings)
    {
        var name = settings.GetType().GetAssemblyName() + ".json";
        if (string.IsNullOrEmpty(name) || name == ".json") return;

        try
        {
            var settingsPath = Path.Combine(Utils.ApplicationPath, "Plugins", name);
            Directory.CreateDirectory(Path.Combine(Utils.ApplicationPath, "Plugins"));
            var json = SettingsProvider.Serialize(settings);
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception e)
        {
            _logger.Error(e, $"Unable to Save Settings for {name}");
        }
    }
}
