using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Plugin.Abstractions.Attributes;

namespace Shoko.Server
{
    public class RenameFileHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public static IDictionary<string, (Type type, string description)> Renamers { get; } = new Dictionary<string, (Type type, string description)>();

        private static IRenameScript _getRenameScript(string name)
        {
            var script = RepoFactory.RenameScript.GetByName(name) ?? RepoFactory.RenameScript.GetDefaultOrFirst();

            return new RenameScriptImpl
            {
                Script = script.Script,
                Type = script.RenamerType,
                ExtraData = script.ExtraData
            };
        }

        public static string GetFilename(SVR_VideoLocal_Place place, string scriptName)
        {
            string result = Path.GetFileName(place.FilePath);
            var script = _getRenameScript(scriptName);

            foreach (var renamer in GetPluginRenamersSorted(script.Type))
            {
                // TODO Error handling and possible deference
                var args = new RenameEventArgs
                {
                    AnimeInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a?.GetAnimeSeries()?.GetAnime())
                        .Where(a => a != null).Cast<IAnime>().ToList(),
                    GroupInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()?.AnimeGroup)
                        .Where(a => a != null).DistinctBy(a => a.AnimeGroupID).Cast<IGroup>().ToList(),
                    EpisodeInfo = place.VideoLocal?.GetAnimeEpisodes().Where(a => a != null).Cast<IEpisode>().ToList(),
                    FileInfo = place,
                    Script = script,
                };
                var res = renamer.GetFilename(args);
                if (args.Cancel) return null;
                if (string.IsNullOrEmpty(res)) continue;
                return res;
            }

            return result;
        }
        
        public static (ImportFolder, string) GetDestination(SVR_VideoLocal_Place place, string scriptName)
        {
            var script = _getRenameScript(scriptName);

            // TODO Error handling and possible deference
            foreach (var renamer in GetPluginRenamersSorted(script.Type))
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
                        .Where(a => a.DropFolderType != DropFolderType.Excluded).ToList(),
                    Script = script,
                };
                (IImportFolder destFolder, string destPath) = renamer.GetDestination(args);
                if (args.Cancel) return (null, null);
                if (string.IsNullOrEmpty(destPath) || destFolder == null) continue;
                var importFolder = RepoFactory.ImportFolder.GetByImportLocation(destFolder.Location);
                if (importFolder != null) return (importFolder, destPath);
                logger.Error(
                    $"Renamer returned a Destination Import Folder, but it could not be found. The offending plugin was {renamer.GetType().GetAssemblyName()} renamer was {renamer.GetType().Name}");
            }

            return (null, null);
        }
        
        internal static void FindRenamers(IList<Assembly> assemblies)
        {
            // TODO Move Legacy Rename Script handling to the legacy renamer
            var allTypes = assemblies.SelectMany(a => 
                {
                    try
                    {
                        return a.GetTypes();
                    } 
                    catch
                    {
                        return new Type[0];
                    }
                }).Where(a => a.GetInterfaces().Contains(typeof(IRenamer))).ToList();

            foreach (var implementation in allTypes)
            {
                IEnumerable<RenamerAttribute> attributes = implementation.GetCustomAttributes<RenamerAttribute>();
                foreach ((string key, string desc) in attributes.Select(a => (key: a.RenamerId, desc: a.Description)))
                {
                    if (key == null) continue;
                    if (Renamers.ContainsKey(key))
                    {
                        logger.Warn(
                            $"[RENAMER] Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.Assembly.Location} and {Renamers[key]}@{Renamers[key].type.Assembly.Location}");
                        continue;
                    }

                    Renamers.Add(key, (implementation, desc));
                }
            }
        }

        public static IList<IRenamer> GetPluginRenamersSorted(string renamerName) => 
            _getEnabledRenamers(renamerName).OrderBy(a => renamerName == a.Key ? int.MaxValue : 0)
                .ThenBy(a => ServerSettings.Instance.Plugins.Priority.Contains(a.Key) ? ServerSettings.Instance.Plugins.Priority.IndexOf(a.Key) : int.MaxValue)
                .ThenBy(a => a.Key)
                .Select(a => (IRenamer)ActivatorUtilities.CreateInstance(ShokoServer.ServiceContainer, a.Value.type)).ToList();

        private static IEnumerable<KeyValuePair<string, (Type type, string description)>> _getEnabledRenamers(string renamerName)
        {
            foreach(var kvp in Renamers)
            {
                if (kvp.Key != renamerName && ServerSettings.Instance.Plugins.EnabledRenamers.TryGetValue(kvp.Key, out bool isEnabled) && !isEnabled)
                    continue;

                yield return kvp;
            }
        }
    }
}