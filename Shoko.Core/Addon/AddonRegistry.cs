using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NLog;

namespace Shoko.Core.Addon
{
    internal class AddonRegistry
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        internal static Dictionary<string, IPlugin> Plugins { get; private set; } = new Dictionary<string, IPlugin>();

        public static void Initalize()
        {
            List<Assembly> asse = new List<Assembly>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            UriBuilder uri = new UriBuilder(assembly.GetName().CodeBase);
            string dirname = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
            asse.Add(Assembly.GetCallingAssembly()); //add this to dynamically load as well.
            foreach (string dll in Directory.GetFiles(dirname, $"*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    asse.Add(Assembly.LoadFile(dll));
                }
                catch (FileLoadException)
                {
                }
                catch (BadImageFormatException)
                {
                }
            }

            var implementations = asse.SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (Exception)
                    {
                        return new Type[0];
                    }
                });

            LoadPlugins(implementations);
        }

        private static void LoadPlugins(IEnumerable<Type> typesToScan) 
        {

            foreach (var implementation in typesToScan.Where(a => a.GetInterfaces().Contains(typeof(IPlugin))))
            {
                IEnumerable<PluginAttribute> attributes = implementation.GetCustomAttributes<PluginAttribute>();
                foreach (string id in attributes.Select(a => a.PluginID))
                {
                    if (id == null) continue;
                    if (Plugins.ContainsKey(id))
                    {
                        logger.Warn($"[PluginLoader] Warning Duplicate Plugin ID \"{id}\" of types {implementation}@{implementation.Assembly.Location} and {Plugins[id]}@{Plugins[id].GetType().Assembly.Location}");
                        continue;
                    }

                    //create instaince of using a Constructor injection method.
                    Plugins.Add(id, (IPlugin) Activator.CreateInstance(implementation));
                }
            }
        }
        //TODO: Redesigin renamers, should just be a 2 function interface, GetDirectory(VideoLocal):(ImportFolder, string) and GetName(VideoLocal):string
        /* 
        private static void LoadRenamers(IEnumerable<Type> typesToScan) 
        {

            foreach (var implementation in typesToScan.Where(a => a.GetInterfaces().Contains(typeof(IRenamer))))
            {
                IEnumerable<PluginAttribute> attributes = implementation.GetCustomAttributes<PluginAttribute>();
                foreach (string id in attributes.Select(a => a.PluginID))
                {
                    if (id == null) continue;
                    if (Plugins.ContainsKey(id))
                    {
                        logger.Warn($"[PluginLoader] Warning Duplicate Plugin ID \"{id}\" of types {implementation}@{implementation.Assembly.Location} and {Plugins[id]}@{Plugins[id].GetType().Assembly.Location}");
                        continue;
                    }

                    //create instaince of using a Constructor injection method.
                    Plugins.Add(id, (IPlugin) Activator.CreateInstance(implementation));
                }
            }
        }*/
    }
}
