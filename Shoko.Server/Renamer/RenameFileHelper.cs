using System;

using NLog;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nancy.Extensions;

namespace Shoko.Server
{
    public class RenameFileHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static  IDictionary<string, Type> ScriptImplementations = new Dictionary<string, Type>();

        public static IRenamer GetRenamer()
        {
            var script = RepoFactory.RenameScript.GetDefaultScript();
            if (script == null) return null;
            return GetRenamerFor(script);
        }

        public static IRenamer GetRenamerWithFallback()
        {
            var script = RepoFactory.RenameScript.GetDefaultOrFirst();
            if (script == null) return null;

            return GetRenamerFor(script);
        }

        public static IRenamer GetRenamer(string scriptName)
        {
            var script = RepoFactory.RenameScript.GetByName(scriptName);
            if (script == null) return null;

            return GetRenamerFor(script);
        }

        private static IRenamer GetRenamerFor(RenameScript script)
        {
            if (!ScriptImplementations.ContainsKey(script.RenamerType))
                return null;

            return (IRenamer) Activator.CreateInstance(ScriptImplementations[script.RenamerType], script);
        }

        public static void InitialiseRenamers()
        {
            List<Assembly> asse = new List<Assembly>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            UriBuilder uri = new UriBuilder(assembly.GetName().CodeBase);
            string dirname = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
            asse.Add(Assembly.GetCallingAssembly()); //add this to dynamically load as well.
            foreach (string dll in Directory.GetFiles(dirname, $"Renamer.*.dll", SearchOption.AllDirectories))
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

            var implementations = asse.SelectMany(a => a.GetTypes())
                .Where(a => a.GetInterfaces().Contains(typeof(IRenamer)));

            foreach (var implementation in implementations)
            {
                IEnumerable<RenamerAttribute> attributes = implementation.GetCustomAttributes<RenamerAttribute>();
                foreach (string key in attributes.Select(a => a.RenamerId))
                {
                    if (key == null) continue;
                    if (ScriptImplementations.ContainsKey(key))
                    {
                        logger.Warn($"[RENAMER] Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.GetAssemblyPath()} and {ScriptImplementations[key]}@{ScriptImplementations[key].GetAssemblyPath()}");
                        continue;
                    }
                    ScriptImplementations.Add(key, implementation);
                }
            }
        }
        public static string GetNewFileName(SVR_VideoLocal_Place vid)
        {
            try
            {
                return GetRenamer()?.GetFileName(vid);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return string.Empty;
            }
        }
    }
}