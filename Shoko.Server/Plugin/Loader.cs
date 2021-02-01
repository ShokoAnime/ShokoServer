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

namespace Shoko.Server.Plugin
{
    public class Loader : ISettingsProvider
    {
        public static Loader Instance { get; } = new Loader();
        public IDictionary<Type, IPlugin> Plugins { get; } = new Dictionary<Type, IPlugin>();
        private readonly IList<Type> _pluginTypes = new List<Type>();
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        
        internal void Load(IServiceCollection serviceCollection)
        {
            var assemblies = new List<Assembly>();
            var assembly = Assembly.GetExecutingAssembly();
            var uri = new UriBuilder(assembly.GetName().CodeBase);
            var dirname = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
            // if (dirname == null) return;
            assemblies.Add(Assembly.GetCallingAssembly()); //add this to dynamically load as well.
                        
            //Load plugins from the user config dir too.
            var userPluginDir = Path.Combine(ServerSettings.ApplicationPath, "plugins");
            var userPlugins = Directory.Exists(userPluginDir) ? Directory.GetFiles(userPluginDir, "*.dll", SearchOption.AllDirectories) : new string[0];
            
            foreach (var dll in userPlugins.Concat(Directory.GetFiles(dirname, "plugins/*.dll", SearchOption.AllDirectories)))
            {
                try
                {
                    string name = Path.GetFileNameWithoutExtension(dll);
                    if (ServerSettings.Instance.Plugins.EnabledPlugins.ContainsKey(name) &&
                        !ServerSettings.Instance.Plugins.EnabledPlugins[name])
                    {
                        Logger.Info($"Found {name}, but it is disabled in the Server Settings. Skipping it.");
                        continue;
                    }
                    Logger.Debug($"Trying to load {dll}");
                    assemblies.Add(Assembly.LoadFrom(dll));
                }
                catch (FileLoadException)
                {
                    Logger.Debug("BadImageFormatException");
                }
                catch (BadImageFormatException)
                {
                    Logger.Debug("BadImageFormatException");
                }
            }

            RenameFileHelper.FindRenamers(assemblies);
            LoadPlugins(assemblies, serviceCollection);
        }

        private void LoadPlugins(IEnumerable<Assembly> assemblies, IServiceCollection serviceCollection)
        {
            var implementations = assemblies.SelectMany(a => {
                try
                {
                    var types = a.GetTypes().Where(a => a.GetInterfaces().Contains(typeof(IPlugin))).ToArray();
                    if (a != Assembly.GetExecutingAssembly() && types.Length > 0)
                    {
                        var name = Path.GetFileNameWithoutExtension(a.Location);
                        ServerSettings.Instance.Plugins.EnabledPlugins.TryAdd(name, true);
                        if (!ServerSettings.Instance.Plugins.Priority.Contains(name))
                            ServerSettings.Instance.Plugins.Priority.Add(name);
                    }
                    return types;
                }
                catch (Exception e)
                {
                    Logger.Debug(e);
                    return Array.Empty<Type>();
                }
            });
            ServerSettings.Instance.SaveSettings();
            
            foreach (var implementation in implementations)
            {
                var mtd = implementation.GetMethod("ConfigureServices",  BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (mtd != null)
                    mtd.Invoke(null, new object[]{serviceCollection});

                _pluginTypes.Add(implementation);
            }
        }

        internal void InitPlugins(IServiceProvider provider)
        {
            Logger.Info("Loading {0} plugins", _pluginTypes.Count);

            foreach (var pluginType in _pluginTypes)
            {
                var plugin = (IPlugin)ActivatorUtilities.CreateInstance(provider, pluginType);
                Plugins.Add(pluginType, plugin);
                LoadSettings(pluginType, plugin);
                Logger.Info($"Loaded: {plugin.Name}");
                plugin.Load();
            }
            // When we initialized the plugins, we made entries for the Enabled State of Plugins
            ServerSettings.Instance.SaveSettings();
        }

        private void LoadSettings(Type type, IPlugin plugin)
        {
            (string name, Type t) = type.Assembly.GetTypes()
                .Where(p => p.IsClass && typeof(IPluginSettings).IsAssignableFrom(p))
                .DistinctBy(a => a.GetAssemblyName())
                .Select(a => (a.GetAssemblyName() + ".json", a)).FirstOrDefault();
            if (string.IsNullOrEmpty(name) || name == ".json") return;
            
            try
            {
                if (ServerSettings.Instance.Plugins.EnabledPlugins.ContainsKey(name) && !ServerSettings.Instance.Plugins.EnabledPlugins[name])
                    return;
                string settingsPath = Path.Combine(ServerSettings.ApplicationPath, "Plugins", name);
                object obj = !File.Exists(settingsPath)
                    ? Activator.CreateInstance(t)
                    : ServerSettings.Deserialize(t, File.ReadAllText(settingsPath));
                // Plugins.Settings will be empty, since it's ignored by the serializer
                var settings = (IPluginSettings) obj;
                ServerSettings.Instance.Plugins.Settings.Add(settings);

                plugin.OnSettingsLoaded(settings);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Unable to initialize Settings for {name}");
            }
        }

        public void SaveSettings(IPluginSettings settings)
        {
            string name = settings.GetType().GetAssemblyName() + ".json";
            if (string.IsNullOrEmpty(name) || name == ".json") return;

            try
            {
                string settingsPath = Path.Combine(ServerSettings.ApplicationPath, "Plugins", name);
                Directory.CreateDirectory(Path.Combine(ServerSettings.ApplicationPath, "Plugins"));
                string json = ServerSettings.Serialize(settings);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Unable to Save Settings for {name}");
            }
        }
    }
}