using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autofac;
using NLog;
using Shoko.Core.Extensions;

namespace Shoko.Core.Addon
{
    internal class AddonRegistry
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        internal static Dictionary<string, IPlugin> Plugins { get; private set; } = new Dictionary<string, IPlugin>();
        //internal static Dictionary<string, Type> PluginConfigTypes { get; private set; } = new Dictionary<string, Type>();
        private static Dictionary<string, Type> PluginTypes { get; set; } = new Dictionary<string, Type>();
        internal static Dictionary<Assembly, string> AssemblyToPluginMap { get; private set; } = new Dictionary<Assembly, string>();

        internal static bool Initalized { get; private set; } = false;

        public static void Init()
        {
            if (Initalized) throw new InvalidOperationException("Plugin loader has already ran");
            foreach ((string id, Type type) in PluginTypes)
            {
                Plugins.Add(id, (IPlugin)ShokoServer.AutofacContainer.Resolve(type));
            }
            Initalized = true;
        }

        public static void LoadPluigins()
        {
            if (Initalized) throw new InvalidOperationException("Plugin loader has already ran");

            List<Assembly> asse = new List<Assembly>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            string dirname = Path.GetDirectoryName(assembly.Location);
            asse.Add(Assembly.GetCallingAssembly()); //add this to dynamically load as well.
            foreach (string dll in Directory.GetFiles(dirname, $"*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    asse.Add(Assembly.LoadFile(dll));
                }
                catch (FileLoadException) {}
                catch (BadImageFormatException) {}
            }

            var types = asse.SelectMany(a =>
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
            ParsePlugins(types);
        }

        private static void ParsePlugins(IEnumerable<Type> typesToScan)
        {
            foreach (var implementation in typesToScan.Where(a => a.GetInterfaces().Contains(typeof(IPlugin))))
            {
                IEnumerable<PluginAttribute> attributes = implementation.GetCustomAttributes<PluginAttribute>();
                if (attributes.Count() == 0) logger.Error($"[PluginLoader] {implementation.FullName}.{implementation}@{implementation.Assembly.Location} is missing the Plugin attribute.");
                foreach (string id in attributes.Select(a => a?.PluginID))
                {
                    if (id == null) continue;
                    if (Plugins.ContainsKey(id))
                    {
                        logger.Warn($"[PluginLoader] Warning Duplicate Plugin ID \"{id}\" of types {implementation.FullName}.{implementation}@{implementation.Assembly.Location} and {Plugins[id]}@{Plugins[id].GetType().Assembly.Location}");
                        continue;
                    }

                    PluginTypes.Add(id, implementation);

                    //PluginConfigTypes.Add(id, implementation.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPlugin<>)).First().GetGenericArguments()[0]);

                    if (!AssemblyToPluginMap.ContainsKey(implementation.Assembly))
                        AssemblyToPluginMap.Add(implementation.Assembly, id);
                    else
                        logger.Info($"[PluginLoader] Multiple plugins contained within the assembly: {implementation.Assembly.Location} this could cause errors with the DbContext generation.");
                }
            }
        }

        internal static void RegisterAutofac(ContainerBuilder builder)
        {
            foreach ((string id, Type type) in PluginTypes)
            {
                RegisterPluginForAutofac(builder, type, id);
            }
        }

        /// <summary>
        /// Register the types with Autofac, to be created at a later
        /// </summary>
        /// <param name="builder">The autofac ContainerBuilder to use.</param>
        /// <param name="typesToScan">The types to go through and scan.</param>
        private static void RegisterPluginForAutofac(ContainerBuilder builder, Type implementation, string id) 
        {
            foreach (var mtd in implementation.GetMethods().Where(m => m.GetCustomAttribute(typeof(AutofacRegistrationMethodAttribute)) != null))
            {
                if (!mtd.IsStatic)
                {
                    logger.Error($"[PluginLoader] Error: Plugin \"{id}\" {implementation.FullName}.{mtd.Name} needs to be static for it to register in Autofac, please contact the developer to get this resolved.");
                    continue;
                }
                var paramaters = mtd.GetParameters();
                if (paramaters.Length != 1 || paramaters[0].ParameterType != typeof(ContainerBuilder))
                {
                    logger.Error($"[PluginLoader] Error: Plugin \"{id}\" provided an invalid Autofac registration method, please contact the developer to get this resolved.");
                    continue;
                }

                mtd.Invoke(null, new[] { builder });
            }

            builder.RegisterType(implementation).SingleInstance();
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
