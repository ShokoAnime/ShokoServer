using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Utilities;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Renamer;

public class RenameFileService
{
    private readonly ILogger<RenameFileService> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly RenamerInstanceRepository _renamers;
    public Dictionary<string, IRenamer<MoveRenameEventArgs>> RenamersByKey { get; } = [];
    public Dictionary<Type, IRenamer<MoveRenameEventArgs>> RenamersByType { get; } = [];

    public RenameFileService(ILogger<RenameFileService> logger, ISettingsProvider settingsProvider, RenamerInstanceRepository renamers)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _renamers = renamers;
        LoadRenamers(AppDomain.CurrentDomain.GetAssemblies());
    }

    public MoveRenameResult GetNewPath(SVR_VideoLocal_Place place, RenamerInstance? renamerInstance = null, bool? move = null, bool? rename = null)
    {
        var settings = _settingsProvider.GetSettings();
        var shouldMove = move ?? settings.Import.MoveOnImport;
        var shouldRename = rename ?? settings.Import.RenameOnImport;

        var videoLocal = place.VideoLocal ??
            throw new NullReferenceException(nameof(place.VideoLocal));
        var xrefs = videoLocal.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.AniDBEpisode)
            .WhereNotNull()
            .ToList();

        // We don't have all the data yet, so don't try to rename yet.
        if (xrefs.Count != episodes.Count)
            return new MoveRenameResult
            {
                Error = new MoveRenameError(
                    $"Not enough data to do renaming for the recognized file. Missing metadata for {xrefs.Count - episodes.Count} episodes. Aborting.")
            };

        if (renamerInstance == null)
        {
            var defaultRenamerName = settings.Import.DefaultRenamer;
            if (string.IsNullOrWhiteSpace(defaultRenamerName)) return new MoveRenameResult { Error = new MoveRenameError("No default renamer configured and no renamer instance given") };
            var defaultRenamer = _renamers.GetByName(defaultRenamerName);
            if (defaultRenamer == null) return new MoveRenameResult { Error = new MoveRenameError("The specified default renamer does not exist") };
            renamerInstance = defaultRenamer;
        }

        if (!RenamersByType.TryGetValue(renamerInstance.Type, out var renamer)) return new MoveRenameResult { Error = new MoveRenameError($"No renamers configured for {renamerInstance.Type}") };
        // check if it's unrecognized
        if (xrefs.Count == 0 && renamer is not IUnrecognizedRenamer) return new MoveRenameResult { Error = new MoveRenameError("Configured renamer does not support unrecognized files, and the file is unrecognized") };

        var anime = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AniDBAnime)
            .WhereNotNull()
            .ToList();
        var groups = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        var availableFolders = RepoFactory.ImportFolder.GetAll()
            .Cast<IImportFolder>()
            .Where(a => a.DropFolderType != DropFolderType.Excluded)
            .ToList();

        MoveRenameEventArgs args;
        var renamerInterface = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType() && a == typeof(IRenamer<>))!;
        if (renamerInterface.GetGenericArguments()[0].IsGenericType)
        {
            var settingsType = renamerInterface.GetGenericArguments()[0].GetGenericArguments()[0];
            if (settingsType != renamerInstance.Settings.GetType())
                return new MoveRenameResult
                {
                    Error = new MoveRenameError(
                        $"Configured renamer has settings of type {settingsType} but the renamer instance has settings of type {renamerInstance.Settings.GetType()}")
                };

            var argsType = typeof(MoveRenameEventArgs<>).MakeGenericType();
            args = (MoveRenameEventArgs)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, argsType);
            args.AnimeInfo = anime;
            args.FileInfo = place;
            args.EpisodeInfo = episodes;
            args.GroupInfo = groups;
            args.AvailableFolders = availableFolders;
            args.MoveEnabled = shouldMove;
            args.RenameEnabled = shouldRename;
            argsType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(a => a.Name == "Settings")!.SetValue(args,
                renamerInstance.Settings);
        }
        else
        {
            args = new MoveRenameEventArgs
            {
                AnimeInfo = anime,
                FileInfo = place,
                EpisodeInfo = episodes,
                GroupInfo = groups,
                AvailableFolders = availableFolders,
                MoveEnabled = move ?? settings.Import.MoveOnImport,
                RenameEnabled = rename ?? settings.Import.RenameOnImport,
            };
        }

        try
        {
            // get filename from plugin
            var res = renamer.GetNewPath(args);
            if (res.Error != null) return res;

            // if the plugin said to cancel, then do so
            if (args.Cancel)
                return new MoveRenameResult
                {
                    Error = new MoveRenameError($"Operation canceled by renamer {renamer.GetType().Name}.")
                };

            // TODO check fallback renamer 
            if (shouldRename && string.IsNullOrEmpty(res.FileName))
                return new MoveRenameResult
                {
                    Error = new MoveRenameError($"Set to rename, but renamer {renamer.GetType().Name} did not return a new file name")
                };

            if (shouldMove && (string.IsNullOrEmpty(res.Path) || res.DestinationImportFolder == null))
                return new MoveRenameResult
                {
                    Error = new MoveRenameError($"Renamer {renamer.GetType().Name} did not return a file path")
                };

            return res;
        }
        catch (Exception e)
        {
            return new MoveRenameResult
            {
                Error = new MoveRenameError(e.Message, e)
            };
        }
    }

    private void LoadRenamers(IList<Assembly> assemblies)
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
            .Where(a => a.GetInterfaces().Any(b => b.IsGenericType && b.GetGenericTypeDefinition() == typeof(IRenamer<>)))
            .ToList();

        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerAttribute>();
            foreach (var (key, desc) in attributes.Select(a => (key: a.RenamerId, desc: a.Description)))
            {
                if (key is null)
                    continue;

                var version = implementation.Assembly.GetName().Version;
                if (RenamersByKey.TryGetValue(key, out var value))
                {
                    var info = value.GetType();
                    _logger.LogWarning($"Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.Assembly.Location} (v{version}) and {value}@{info.Assembly.Location} (v{info.Assembly.GetName().Version})");
                    continue;
                }

                var renamer = CreateRenamer<MoveRenameEventArgs>(implementation);
                if (renamer is null)
                {
                    _logger.LogWarning($"Could not create renamer of type: {implementation}@{implementation.Assembly.Location} (v{version})");
                    continue;
                }

                _logger.LogInformation($"Added Renamer: {key} (v{version}) - {desc}");
                RenamersByKey.Add(key, renamer);
                RenamersByType.Add(implementation, renamer);
            }
        }
    }

    public IRenamer<T> CreateRenamer<T>(Type type) where T : MoveRenameEventArgs
    {
        return (IRenamer<T>)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, type);
    }
}
