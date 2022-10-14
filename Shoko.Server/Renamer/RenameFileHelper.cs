using System;
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

namespace Shoko.Server;

public class RenameFileHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static IDictionary<string, (Type type, string description)> Renamers { get; } =
        new Dictionary<string, (Type type, string description)>();

    private static IRenameScript _getRenameScript(string name)
    {
        var script = RepoFactory.RenameScript.GetByName(name) ?? RepoFactory.RenameScript.GetDefaultScript();
        if (script == null)
        {
            return null;
        }

        return new RenameScriptImpl { Script = script.Script, Type = script.RenamerType, ExtraData = script.ExtraData };
    }

    private static IRenameScript _getRenameScriptWithFallback(string name)
    {
        var script = RepoFactory.RenameScript.GetByName(name) ?? RepoFactory.RenameScript.GetDefaultOrFirst();
        if (script == null)
        {
            return null;
        }

        return new RenameScriptImpl { Script = script.Script, Type = script.RenamerType, ExtraData = script.ExtraData };
    }

    public static string GetFilename(SVR_VideoLocal_Place place, string scriptName)
    {
        var result = Path.GetFileName(place.FilePath);
        var script = _getRenameScript(scriptName);
        var args = new RenameEventArgs
        {
            AnimeInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a?.GetAnimeSeries()?.GetAnime())
                .Where(a => a != null).Cast<IAnime>().ToList(),
            GroupInfo = place.VideoLocal?.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()?.AnimeGroup)
                .Where(a => a != null).DistinctBy(a => a.AnimeGroupID).Cast<IGroup>().ToList(),
            EpisodeInfo = place.VideoLocal?.GetAnimeEpisodes().Where(a => a != null).Cast<IEpisode>().ToList(),
            FileInfo = place,
            Script = script
        };

        foreach (var renamer in GetPluginRenamersSorted(script?.Type))
        {
            try
            {
                // get filename from plugin
                var res = renamer.GetFilename(args);
                // if the plugin said to cancel, then do so
                if (args.Cancel)
                {
                    return null;
                }

                // if the plugin returned no name, then defer
                if (string.IsNullOrEmpty(res))
                {
                    continue;
                }

                return res;
            }
            catch (Exception e)
            {
                if (!ServerSettings.Instance.Plugins.DeferOnError || args.Cancel)
                {
                    throw;
                }

                Logger.Warn(
                    $"Renamer: {renamer.GetType().Name} threw an error while renaming, deferring to next renamer. Filename: \"{result}\" Error message: \"{e.Message}\"");
            }
        }

        return result;
    }

    public static (ImportFolder, string) GetDestination(SVR_VideoLocal_Place place, string scriptName)
    {
        var script = _getRenameScriptWithFallback(scriptName);

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
            Script = script
        };

        foreach (var renamer in GetPluginRenamersSorted(script?.Type))
        {
            try
            {
                // get destination from renamer
                var (destFolder, destPath) = renamer.GetDestination(args);
                // if the renamer has said to cancel, then return null
                if (args.Cancel)
                {
                    return (null, null);
                }

                // if no path was specified, then defer
                if (string.IsNullOrEmpty(destPath) || destFolder == null)
                {
                    continue;
                }

                if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
                {
                    destPath = destPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                }

                destPath = RemoveFilename(place.FilePath, destPath);

                var importFolder = RepoFactory.ImportFolder.GetByImportLocation(destFolder.Location);
                if (importFolder == null)
                {
                    Logger.Error(
                        $"Renamer returned a Destination Import Folder, but it could not be found. The offending plugin was: {renamer.GetType().GetAssemblyName()} with renamer: {renamer.GetType().Name}");
                    continue;
                }

                return (importFolder, destPath);
            }
            catch (Exception e)
            {
                if (!ServerSettings.Instance.Plugins.DeferOnError || args.Cancel)
                {
                    throw;
                }

                Logger.Warn(
                    $"Renamer: {renamer.GetType().Name} threw an error while moving, deferring to next renamer. Path: \"{place.FullServerPath}\" Error message: \"{e.Message}\"");
            }
        }

        return (null, null);
    }

    private static string RemoveFilename(string filePath, string destPath)
    {
        var name = Path.DirectorySeparatorChar + Path.GetFileName(filePath);
        var last = destPath.LastIndexOf(Path.DirectorySeparatorChar);

        if (last <= -1 || last >= destPath.Length - 1)
        {
            return destPath;
        }

        var end = destPath[last..];
        if (end.Equals(name, StringComparison.Ordinal))
        {
            destPath = destPath[..last];
        }

        return destPath;
    }

    internal static void FindRenamers(IList<Assembly> assemblies)
    {
        var allTypes = assemblies.SelectMany(a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch
            {
                return Type.EmptyTypes;
            }
        }).Where(a => a.GetInterfaces().Contains(typeof(IRenamer))).ToList();

        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerAttribute>();
            foreach (var (key, desc) in attributes.Select(a => (key: a.RenamerId, desc: a.Description)))
            {
                if (key == null)
                {
                    continue;
                }

                if (Renamers.ContainsKey(key))
                {
                    Logger.Warn(
                        $"[RENAMER] Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.Assembly.Location} and {Renamers[key]}@{Renamers[key].type.Assembly.Location}");
                    continue;
                }

                Renamers.Add(key, (implementation, desc));
            }
        }
    }

    public static IList<IRenamer> GetPluginRenamersSorted(string renamerName)
    {
        return _getEnabledRenamers(renamerName).OrderBy(a => renamerName == a.Key ? 0 : int.MaxValue)
            .ThenBy(a =>
                ServerSettings.Instance.Plugins.RenamerPriorities.ContainsKey(a.Key)
                    ? ServerSettings.Instance.Plugins.RenamerPriorities[a.Key]
                    : int.MaxValue)
            .ThenBy(a => a.Key, StringComparer.InvariantCulture)
            .Select(a => (IRenamer)ActivatorUtilities.CreateInstance(ShokoServer.ServiceContainer, a.Value.type))
            .ToList();
    }

    private static IEnumerable<KeyValuePair<string, (Type type, string description)>> _getEnabledRenamers(
        string renamerName)
    {
        foreach (var kvp in Renamers)
        {
            if (!string.IsNullOrEmpty(renamerName) && kvp.Key != renamerName)
            {
                continue;
            }

            if (ServerSettings.Instance.Plugins.EnabledRenamers.TryGetValue(kvp.Key, out var isEnabled) && !isEnabled)
            {
                continue;
            }

            yield return kvp;
        }
    }
}
