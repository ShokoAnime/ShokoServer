using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using IRenamer = Shoko.Server.Renamer.IRenamer;

namespace Shoko.Server
{
    public class RenameFileHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static IDictionary<string, Type> LegacyScriptImplementations = new Dictionary<string, Type>();
        public static IDictionary<string, string> LegacyScriptDescriptions { get; } = new Dictionary<string, string>();

        public static string GetFilename(SVR_VideoLocal_Place place)
        {
            string result = Path.GetFileName(place.FilePath);

            foreach (var renamer in GetPluginRenamersSorted())
            {
                var args = new RenameEventArgs
                {
                    AnimeInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a?.GetAnimeSeries()?.GetAnime())
                        .Where(a => a != null).Cast<IAnime>().ToList(),
                    GroupInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()?.AnimeGroup)
                        .Where(a => a != null).DistinctBy(a => a.AnimeGroupID).Cast<IGroup>().ToList(),
                    EpisodeInfo = place.VideoLocal?.GetAnimeEpisodes().Where(a => a != null).Cast<IEpisode>().ToList(),
                    FileInfo = place
                };
                renamer.GetFilename(args);
                if (args.Cancel) return null;
                if (string.IsNullOrEmpty(args.Result)) continue;
                return args.Result;
            }

            string attempt = GetRenamerWithFallback()?.GetFileName(place);
            if (attempt != null) result = attempt;

            return result;
        }
        
        public static (ImportFolder, string) GetDestination(SVR_VideoLocal_Place place)
        {
            foreach (var renamer in GetPluginRenamersSorted())
            {
                var args = new MoveEventArgs
                {
                    AnimeInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a?.GetAnimeSeries()?.GetAnime())
                        .Where(a => a != null).Cast<IAnime>().ToList(),
                    GroupInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()?.AnimeGroup)
                        .Where(a => a != null).DistinctBy(a => a.AnimeGroupID).Cast<IGroup>().ToList(),
                    EpisodeInfo = place.VideoLocal?.GetAnimeEpisodes().Where(a => a != null).Cast<IEpisode>().ToList(),
                    FileInfo = place,
                    AvailableFolders = RepoFactory.ImportFolder.GetAll().Cast<IImportFolder>()
                        .Where(a => a.DropFolderType != DropFolderType.Excluded).ToList()
                };
                renamer.GetDestination(args);
                if (args.Cancel) return (null, null);
                if (string.IsNullOrEmpty(args.DestinationPath) || args.DestinationImportFolder == null) continue;
                var importFolder = RepoFactory.ImportFolder.GetByImportLocation(args.DestinationPath);
                if (importFolder != null) return (importFolder, args.DestinationPath);
                logger.Error(
                    $"Renamer returned a Destination Import Folder, but it could not be found. The offending plugin was {renamer.GetType().GetAssemblyName()}");
            }

            (ImportFolder dest, string folder) = GetRenamerWithFallback().GetDestinationFolder(place);
            if (dest != null && !string.IsNullOrEmpty(folder)) return (dest, folder);

            return (null, null);
        }
        
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
            if (!LegacyScriptImplementations.ContainsKey(script.RenamerType))
                return null;

            try
            {
                return (IRenamer) Activator.CreateInstance(LegacyScriptImplementations[script.RenamerType], script);
            }
            catch (MissingMethodException)
            {
                return (IRenamer)Activator.CreateInstance(LegacyScriptImplementations[script.RenamerType]);
            }
        }
        
        internal static void FindRenamers(IList<Assembly> assemblies)
        {
            var implementations = assemblies.SelectMany(a => 
                {
                    try
                    {
                        return a.GetTypes();
                    } 
                    catch
                    {
                        return new Type[0];
                    }
                })
                .Where(a => a.GetInterfaces().Contains(typeof(IRenamer)));

            foreach (var implementation in implementations)
            {
                IEnumerable<RenamerAttribute> attributes = implementation.GetCustomAttributes<RenamerAttribute>();
                foreach ((string key, string desc) in attributes.Select(a => (key: a.RenamerId, desc: a.Description)))
                {
                    if (key == null) continue;
                    if (LegacyScriptImplementations.ContainsKey(key))
                    {
                        logger.Warn(
                            $"[RENAMER] Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.Assembly.Location} and {LegacyScriptImplementations[key]}@{LegacyScriptImplementations[key].Assembly.Location}");
                        continue;
                    }

                    LegacyScriptImplementations.Add(key, implementation);
                    LegacyScriptDescriptions.Add(key, desc);
                }
            }
        }

        public static List<Shoko.Plugin.Abstractions.IRenamer> GetPluginRenamersSorted()
        {
            return Loader.Instance.Plugins.Values.Where(a =>
                a.GetType().IsAssignableFrom(typeof(Shoko.Plugin.Abstractions.IRenamer))).OrderBy(a =>
            {
                var index = ServerSettings.Instance.Plugins.Priority.IndexOf(a.GetType().GetAssemblyName());
                if (index == -1) index = int.MaxValue;
                return index;
            }).ThenBy(a => a.Name).Cast<Shoko.Plugin.Abstractions.IRenamer>().ToList();
        }
    }
}