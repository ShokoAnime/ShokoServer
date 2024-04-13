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
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Server.Utilities;
using Shoko.Server.Extensions;

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
        var videoLocal = place.VideoLocal ??
            throw new NullReferenceException(nameof(place.VideoLocal));
        var xrefs = videoLocal.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .OfType<SVR_AniDB_Episode>()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .OfType<SVR_AnimeSeries>()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .Cast<IGroup>()
            .ToList();
        var args = new RenameEventArgs(script, place, videoLocal, episodeInfo, animeInfo, groupInfo);
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
                if (!Utils.SettingsProvider.GetSettings().Plugins.DeferOnError || args.Cancel)
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
        var videoLocal = place.VideoLocal ??
            throw new NullReferenceException(nameof(place.VideoLocal));
        var xrefs = videoLocal.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .OfType<SVR_AniDB_Episode>()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .OfType<SVR_AnimeSeries>()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .Cast<IGroup>()
            .ToList();
        var availableFolders = RepoFactory.ImportFolder.GetAll()
            .Cast<IImportFolder>()
            .Where(a => a.DropFolderType != DropFolderType.Excluded)
            .ToList();
        var args = new MoveEventArgs(script, availableFolders, place, videoLocal, episodeInfo, animeInfo, groupInfo);
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
                if (!Utils.SettingsProvider.GetSettings().Plugins.DeferOnError || args.Cancel)
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

                Logger.Info($"Added Renamer: {key} - {desc}");
                Renamers.Add(key, (implementation, desc));
            }
        }
    }

    public static IList<IRenamer> GetPluginRenamersSorted(string renamerName)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return _getEnabledRenamers(renamerName).OrderBy(a => renamerName == a.Key ? 0 : int.MaxValue)
            .ThenBy(a => settings.Plugins.RenamerPriorities.TryGetValue(a.Key, out var priority) ? priority : int.MaxValue)
            .ThenBy(a => a.Key, StringComparer.InvariantCulture)
            .Select(a => (IRenamer)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, a.Value.type))
            .ToList();
    }

    private static IEnumerable<KeyValuePair<string, (Type type, string description)>> _getEnabledRenamers(
        string renamerName)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (string.IsNullOrEmpty(renamerName)) return Renamers;
        return Renamers.Where(kvp =>
            kvp.Key == renamerName && (!settings.Plugins.EnabledRenamers.TryGetValue(kvp.Key, out var isEnabled) || isEnabled));
    }
}
