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
    public class Loader
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
                    // TryAdd, because if it made it this far, then it's missing or true.
                    ServerSettings.Instance.Plugins.EnabledPlugins.TryAdd(name, true);
                    if (!ServerSettings.Instance.Plugins.Priority.Contains(name))
                        ServerSettings.Instance.Plugins.Priority.Add(name);
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

        private void LoadPlugins(IReadOnlyCollection<Assembly> assemblies, IServiceCollection serviceCollection)
        {
            var implementations = assemblies.SelectMany(a => {
                try
                {
                    return a.GetTypes();
                }
                catch (Exception e)
                {
                    Logger.Debug(e);
                    return new Type[0];
                }
            }).Where(a => a.GetInterfaces().Contains(typeof(IPlugin)));
            
            foreach (var implementation in implementations)
            {
                var mtd = implementation.GetMethod("ConfigureServices",  BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (mtd != null)
                    mtd.Invoke(null, new object[]{serviceCollection});

                _pluginTypes.Add(implementation);
            }

            try
            {
                var types = assemblies.SelectMany(a => a.GetTypes()).Select(GetSettingsTypeFromType).Where(a => a != null).ToList();
                foreach (var type in types)
                {
                    serviceCollection.AddTransient(typeof(ISettingsProvider<>), typeof(PluginSettingsProvider<>).MakeGenericType(type));
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error Adding Settings Provider to Service Collection");
            }
        }

        public static Type GetSettingsTypeFromType(Type type)
        {
            var constructors = type.GetConstructors();
            foreach (var constructor in constructors)
            {
                if (!constructor.IsPublic) continue;
                var parameters = constructor.GetParameters();
                foreach (var parameter in parameters)
                {
                    if (parameter.ParameterType.IsInterface && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ISettingsProvider<>))
                    {
                        return parameter.ParameterType.GetGenericArguments().FirstOrDefault();
                    }
                }
            }

            return null;
        }
    }
}