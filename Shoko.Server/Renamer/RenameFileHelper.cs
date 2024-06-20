using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Renamer;

public static class RenameFileHelper
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly Dictionary<string, (Type type, string description, string version)> s_internalRenamers = [];

    public static IReadOnlyDictionary<string, (Type type, string description, string version)> Renamers => s_internalRenamers;

    private static RenameScriptImpl GetRenameScript(int? scriptID)
    {
        var script = (scriptID.HasValue && scriptID.Value is > 0 ? RepoFactory.RenameScript.GetByID(scriptID.Value) : null) ?? RepoFactory.RenameScript.GetDefaultScript();
        if (script is null)
            return new() { Script = string.Empty, Type = string.Empty, ExtraData = string.Empty };
        return new() { Script = script.Script, Type = script.RenamerType, ExtraData = script.ExtraData };
    }

    private static RenameScriptImpl GetRenameScriptWithFallback(int? scriptID)
    {
        var script = (scriptID.HasValue && scriptID.Value is > 0 ? RepoFactory.RenameScript.GetByID(scriptID.Value) : null) ?? RepoFactory.RenameScript.GetDefaultOrFirst();
        if (script is null)
            return new() { Script = string.Empty, Type = string.Empty, ExtraData = string.Empty };
        return new() { Script = script.Script, Type = script.RenamerType, ExtraData = script.ExtraData };
    }

    public static string? GetFilename(SVR_VideoLocal_Place place, int? scriptID)
        => GetFilename(place, GetRenameScript(scriptID));

    public static string? GetFilename(SVR_VideoLocal_Place place, RenameScriptImpl script)
    {
        var videoLocal = place.VideoLocal ??
            throw new NullReferenceException(nameof(place.VideoLocal));
        var xrefs = videoLocal.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();

        // We don't have all the data yet, so don't try to rename yet.
        if (xrefs.Count != episodes.Count)
            return "*Error: Not enough data to do renaming for the recognized file. Missing metadata for {xrefs.Count - episodes.Count} episodes. Aborting.";

        var renamers = GetPluginRenamersSorted(script.Type, xrefs.Count is 0);
        // We don't have a renamer we can use for the file.
        if (renamers.Count is 0)
            return $"*Error: No renamers configured for {(xrefs.Count is 0 ? "unrecognized" : "all")} files. Aborting.";

        var anime = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var groups = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        var availableFolders = RepoFactory.ImportFolder.GetAll()
            .Cast<IImportFolder>()
            .Where(a => a.DropFolderType != DropFolderType.Excluded)
            .ToList();
        var args = new RenameEventArgs(script, availableFolders, place, videoLocal, episodes, anime, groups);
        foreach (var renamer in renamers)
        {
            try
            {
                // get filename from plugin
                var res = renamer.GetFilename(args);
                // if the plugin said to cancel, then do so
                if (args.Cancel)
                    return $"*Error: Operation canceled by renamer {renamer.GetType().Name}.";

                // if the renamer returned no name, then defer to the next renamer.
                if (string.IsNullOrEmpty(res))
                    continue;

                return res;
            }
            catch (Exception e)
            {
                if (!Utils.SettingsProvider.GetSettings().Plugins.DeferOnError || args.Cancel)
                {
                    throw;
                }

                s_logger.Warn(e, $"Renamer {renamer.GetType().Name} threw an error while trying to determine a new file name, deferring to next renamer. File: \"{place.FullServerPath}\" Error message: \"{e.Message}\"");
            }
        }

        return null;
    }

    public static (SVR_ImportFolder? importFolder, string? fileName) GetDestination(SVR_VideoLocal_Place place, int? scriptID)
        => GetDestination(place, GetRenameScriptWithFallback(scriptID));

    public static (SVR_ImportFolder? importFolder, string? fileName) GetDestination(SVR_VideoLocal_Place place, RenameScriptImpl script)
    {
        var videoLocal = place.VideoLocal ??
            throw new NullReferenceException(nameof(place.VideoLocal));
        var xrefs = videoLocal.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();

        // We don't have all the data yet, so don't try to rename yet.
        if (xrefs.Count != episodes.Count)
            return (null, $"*Error: Not enough data to do renaming for the recognized file. Missing metadata for {xrefs.Count - episodes.Count} episodes.");

        var renamers = GetPluginRenamersSorted(script.Type, xrefs.Count is 0);
        // We don't have a renamer we can use for the file.
        if (renamers.Count is 0)
            return (null, $"*Error: No renamers configured for {(xrefs.Count is 0 ? "unrecognized" : "all")} files. Aborting.");

        var anime = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var groups = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        var availableFolders = RepoFactory.ImportFolder.GetAll()
            .Cast<IImportFolder>()
            .Where(a => a.DropFolderType != DropFolderType.Excluded)
            .ToList();
        var args = new MoveEventArgs(script, availableFolders, place, videoLocal, episodes, anime, groups);
        foreach (var renamer in renamers)
        {
            try
            {
                // get destination from renamer
                var (destFolder, destPath) = renamer.GetDestination(args);
                // if the renamer has said to cancel, then return null
                if (args.Cancel)
                    return (null, $"*Error: Operation canceled by renamer {renamer.GetType().Name}.");

                // if no path was specified, then defer
                if (string.IsNullOrEmpty(destPath) || destFolder is null)
                    continue;

                if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
                    destPath = destPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                destPath = RemoveFilename(place.FilePath, destPath);

                var importFolder = RepoFactory.ImportFolder.GetByImportLocation(destFolder.Path);
                if (importFolder is null)
                {
                    s_logger.Warn($"Renamer returned a Destination Import Folder, but it could not be found. The offending plugin was \"{renamer.GetType().GetAssemblyName()}\" with renamer \"{renamer.GetType().Name}\"");
                    continue;
                }

                return (importFolder, destPath);
            }
            catch (Exception e)
            {
                if (!Utils.SettingsProvider.GetSettings().Plugins.DeferOnError || args.Cancel)
                    throw;

                s_logger.Warn($"Renamer: {renamer.GetType().Name} threw an error while finding a destination, deferring to next renamer. Path: \"{place.FullServerPath}\" Error message: \"{e.Message}\"");
            }
        }

        return (null, null);
    }

    private static string RemoveFilename(string filePath, string destPath)
    {
        var name = Path.DirectorySeparatorChar + Path.GetFileName(filePath);
        var last = destPath.LastIndexOf(Path.DirectorySeparatorChar);
        if (last <= -1 || last >= destPath.Length - 1)
            return destPath;

        var end = destPath[last..];
        if (end.Equals(name, StringComparison.Ordinal))
            destPath = destPath[..last];

        return destPath;
    }

    internal static void FindRenamers(IList<Assembly> assemblies)
    {
        var allTypes = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Type.EmptyTypes;
                }
            })
            .Where(a => a.GetInterfaces().Contains(typeof(IRenamer)))
            .ToList();
        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerAttribute>();
            foreach (var (key, desc) in attributes.Select(a => (key: a.RenamerId, desc: a.Description)))
            {
                if (key is null)
                    continue;

                var version = Utils.GetApplicationVersion(implementation.Assembly);
                if (Renamers.TryGetValue(key, out var value))
                {
                    s_logger.Warn($"[RENAMER] Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.Assembly.Location} (v{version}) and {value}@{value.type.Assembly.Location} (v{value.version})");
                    continue;
                }

                s_logger.Info($"Added Renamer: {key} (v{version}) - {desc}");
                s_internalRenamers.Add(key, (implementation, desc, version));
            }
        }
    }

    private static List<IRenamer> GetPluginRenamersSorted(string? renamerName, bool isUnrecognized)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var renamers = GetEnabledRenamers(renamerName)
            .OrderByDescending(a => renamerName == a.Key)
            .ThenBy(a => settings.Plugins.RenamerPriorities.GetValueOrDefault(a.Key, int.MaxValue))
            .ThenBy(a => a.Key, StringComparer.InvariantCulture)
            .Select(a => (IRenamer)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, a.Value.type));
        if (isUnrecognized)
            renamers = renamers.Where(renamer => renamer is IUnrecognizedRenamer);
        return renamers.ToList();
    }

    private static IEnumerable<KeyValuePair<string, (Type type, string description, string version)>> GetEnabledRenamers(string? renamerName)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (string.IsNullOrEmpty(renamerName)) return s_internalRenamers;
        return s_internalRenamers
            .Where(kvp => kvp.Key == renamerName && (!settings.Plugins.EnabledRenamers.TryGetValue(kvp.Key, out var isEnabled) || isEnabled));
    }
}
