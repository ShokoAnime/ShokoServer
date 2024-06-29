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
    private readonly Dictionary<Type, MethodInfo?> _settingsSetters = new();
    private readonly Dictionary<Type, MethodInfo?> _genericGetNewPaths = new();

    public Dictionary<string, IBaseRenamer> RenamersByKey { get; } = [];
    public Dictionary<Type, IBaseRenamer> RenamersByType { get; } = [];
    public Dictionary<IBaseRenamer, bool> AllRenamers { get; } = [];

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
        var shouldMove = move ?? settings.Plugins.Renamer.MoveOnImport;
        var shouldRename = rename ?? settings.Plugins.Renamer.RenameOnImport;

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
            var defaultRenamerName = settings.Plugins.Renamer.DefaultRenamer;
            if (string.IsNullOrWhiteSpace(defaultRenamerName))
                return new MoveRenameResult
                {
                    Error = new MoveRenameError("No default renamer configured and no renamer instance given")
                };
            var defaultRenamer = _renamers.GetByName(defaultRenamerName);
            if (defaultRenamer == null)
                return new MoveRenameResult
                {
                    Error = new MoveRenameError("The specified default renamer does not exist")
                };
            renamerInstance = defaultRenamer;
        }

        if (!RenamersByType.TryGetValue(renamerInstance.Type, out var renamer))
            return new MoveRenameResult
            {
                Error = new MoveRenameError($"No renamers configured for {renamerInstance.Type}")
            };
        // check if it's unrecognized
        if (xrefs.Count == 0 && renamer.GetType().GetInterfaces().Any(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IUnrecognizedRenamer<>)))
            return new MoveRenameResult
            {
                Error = new MoveRenameError("Configured renamer does not support unrecognized files, and the file is unrecognized")
            };

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
        var renamerInterface = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>));
        if (renamerInterface != null)
        {
            var settingsType = renamerInterface.GetGenericArguments()[0];
            if (settingsType != renamerInstance.Settings.GetType())
                return new MoveRenameResult
                {
                    Error = new MoveRenameError(
                        $"Configured renamer has settings of type {settingsType} but the renamer instance has settings of type {renamerInstance.Settings.GetType()}")
                };

            var argsType = typeof(MoveRenameEventArgs<>).MakeGenericType(settingsType);
            args = (MoveRenameEventArgs)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, argsType);
            args.AnimeInfo = anime;
            args.FileInfo = place;
            args.EpisodeInfo = episodes;
            args.GroupInfo = groups;
            args.AvailableFolders = availableFolders;
            args.MoveEnabled = shouldMove;
            args.RenameEnabled = shouldRename;

            // cached reflection
            if (!_settingsSetters.TryGetValue(argsType, out var settingsSetter))
                _settingsSetters.TryAdd(argsType,
                    settingsSetter = argsType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(a => a.Name == "Settings")?.SetMethod);
            if (settingsSetter == null) return new MoveRenameResult {Error = new MoveRenameError($"Cannot find Settings setter on {renamerInterface}")};
            settingsSetter.Invoke(args, [renamerInstance.Settings]);

            if (!_genericGetNewPaths.TryGetValue(renamerInterface, out var method))
                _genericGetNewPaths.TryAdd(renamerInterface,
                    method = renamerInterface.GetMethod(nameof(IRenamer.GetNewPath), BindingFlags.Instance | BindingFlags.Public));

            if (method == null) return new MoveRenameResult {Error = new MoveRenameError("Cannot find GetNewPath method")};

            return GetNewPath((r, a) => (MoveRenameResult)method.Invoke(r, [a])!, renamer, args, shouldRename, shouldMove);
        }

        args = new MoveRenameEventArgs
        {
            AnimeInfo = anime,
            FileInfo = place,
            EpisodeInfo = episodes,
            GroupInfo = groups,
            AvailableFolders = availableFolders,
            MoveEnabled = move ?? settings.Plugins.Renamer.MoveOnImport,
            RenameEnabled = rename ?? settings.Plugins.Renamer.RenameOnImport,
        };

        return GetNewPath((r, a) => ((IRenamer)r).GetNewPath(a), renamer, args, shouldRename, shouldMove);
    }

    /// <summary>
    /// This is called with reflection, so the signature must match the above
    /// </summary>
    /// <param name="func"></param>
    /// <param name="renamer"></param>
    /// <param name="args"></param>
    /// <param name="shouldRename"></param>
    /// <param name="shouldMove"></param>
    /// <returns></returns>
    private static MoveRenameResult GetNewPath(Func<IBaseRenamer, MoveRenameEventArgs, MoveRenameResult> func, IBaseRenamer renamer, MoveRenameEventArgs args, bool shouldRename,
        bool shouldMove)
    {
        try
        {
            // get filename from plugin
            var res = func(renamer, args);
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

        var enabledSetting = _settingsProvider.GetSettings().Plugins.Renamer.EnabledRenamers;
        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerIDAttribute>();
            foreach (var id in attributes.Select(a => a.RenamerId))
            {
                var version = implementation.Assembly.GetName().Version;
                if (RenamersByKey.TryGetValue(id, out var value))
                {
                    var info = value.GetType();
                    _logger.LogWarning(
                        $"Warning Duplicate renamer ID \"{id}\" of types {implementation}@{implementation.Assembly.Location} (v{version}) and {value}@{info.Assembly.Location} (v{info.Assembly.GetName().Version})");
                    continue;
                }

                IBaseRenamer renamer;
                try
                {
                    renamer = (IBaseRenamer)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, implementation);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Could not create renamer of type: {Type}@{Location} (v{Version})", implementation, implementation.Assembly.Location,
                        version);
                    continue;
                }

                if (!enabledSetting.TryGetValue(id, out var enabled) || enabled)
                {
                    _logger.LogInformation($"Added Renamer: {id} (v{version})");
                    RenamersByKey.Add(id, renamer);
                    RenamersByType.Add(implementation, renamer);
                    AllRenamers[renamer] = true;
                }
                else
                    AllRenamers[renamer] = false;
            }
        }
    }
}
