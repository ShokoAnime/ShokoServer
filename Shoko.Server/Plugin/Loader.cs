using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Server.Renamer;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Plugin
{
    public class Loader
    {
        public static IDictionary<Type, IPlugin> Plugins { get; set; } = new Dictionary<Type, IPlugin>();
        private static IList<Type> _pluginTypes = new List<Type>();
        private static ILogger logger = LogManager.GetCurrentClassLogger();
        
        internal static void Load(IServiceCollection serviceCollection)
        {
            var assemblies = new List<Assembly>();
            var assembly = Assembly.GetExecutingAssembly();
            var uri = new UriBuilder(assembly.GetName().CodeBase);
            var dirname = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
            // if (dirname == null) return;
            assemblies.Add(Assembly.GetCallingAssembly()); //add this to dynamically load as well.
            foreach (var dll in Directory.GetFiles(dirname, "plugins/*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    logger.Debug($"Trying to load {dll}");
                    assemblies.Add(Assembly.LoadFrom(dll));
                }
                catch (FileLoadException)
                {
                    logger.Debug("BadImageFormatException");
                }
                catch (BadImageFormatException)
                {
                    logger.Debug("BadImageFormatException");
                }
            }

            RenameFileHelper.FindRenamers(assemblies);
            LoadPlugins(assemblies, serviceCollection);
        }

        private static void LoadPlugins(IEnumerable<Assembly> assemblies, IServiceCollection serviceCollection)
        {
            var implementations = assemblies.SelectMany(a => {
                try
                {
                    return a.GetTypes();
                }
                catch (Exception e)
                {
                    logger.Debug(e);
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
        }

        internal static void InitPlugins(IServiceProvider provider)
        {
            logger.Info("Loading {0} plugins", _pluginTypes.Count);

            foreach (var pluginType in _pluginTypes)
            {
                var plugin = (IPlugin)ActivatorUtilities.CreateInstance(provider, pluginType);
                Plugins.Add(pluginType, plugin);
                logger.Info($"Loaded: {plugin.Name}");
                plugin.Load();
            }
        }
    }
}