﻿using System;
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
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Utilities;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Renamer;

public class RenameFileService
{
    private readonly ILogger<RenameFileService> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly RenamerConfigRepository _renamers;
    private readonly Dictionary<Type, MethodInfo?> _settingsSetters = new();
    private readonly Dictionary<Type, MethodInfo?> _genericGetNewPaths = new();

    public Dictionary<string, IBaseRenamer> RenamersByKey { get; } = [];
    public Dictionary<Type, IBaseRenamer> RenamersByType { get; } = [];
    public Dictionary<IBaseRenamer, bool> AllRenamers { get; } = [];

    public RenameFileService(ILogger<RenameFileService> logger, ISettingsProvider settingsProvider, RenamerConfigRepository renamers)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _renamers = renamers;
        LoadRenamers(AppDomain.CurrentDomain.GetAssemblies());
    }

    public Shoko.Plugin.Abstractions.Events.RelocationResult GetNewPath(SVR_VideoLocal_Place place, RenamerConfig? renamerConfig = null, bool? move = null, bool? rename = null)
    {
        var settings = _settingsProvider.GetSettings();
        var shouldMove = move ?? settings.Plugins.Renamer.MoveOnImport;
        var shouldRename = rename ?? settings.Plugins.Renamer.RenameOnImport;

        var videoLocal = place.VideoLocal ??
                         throw new NullReferenceException(nameof(place.VideoLocal));
        var xrefs = videoLocal.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();

        // We don't have all the data yet, so don't try to rename yet.
        if (xrefs.Count != episodes.Count)
            return new Shoko.Plugin.Abstractions.Events.RelocationResult
            {
                Error = new RelocationError(
                    $"Not enough data to do renaming for the recognized file. Missing metadata for {xrefs.Count - episodes.Count} episodes. Aborting.")
            };

        if (renamerConfig == null)
        {
            var defaultRenamerName = settings.Plugins.Renamer.DefaultRenamer;
            if (string.IsNullOrWhiteSpace(defaultRenamerName))
                return new Shoko.Plugin.Abstractions.Events.RelocationResult
                {
                    Error = new RelocationError("No default renamer configured and no renamer config given")
                };
            var defaultRenamer = _renamers.GetByName(defaultRenamerName);
            if (defaultRenamer == null)
                return new Shoko.Plugin.Abstractions.Events.RelocationResult
                {
                    Error = new RelocationError("The specified default renamer does not exist")
                };
            renamerConfig = defaultRenamer;
        }

        if (!RenamersByType.TryGetValue(renamerConfig.Type, out var renamer))
            return new Shoko.Plugin.Abstractions.Events.RelocationResult
            {
                Error = new RelocationError($"No renamers configured for {renamerConfig.Type}")
            };
        // check if it's unrecognized
        if (xrefs.Count == 0 && renamer.GetType().GetInterfaces().Any(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IUnrecognizedRenamer<>)))
            return new Shoko.Plugin.Abstractions.Events.RelocationResult
            {
                Error = new RelocationError("Configured renamer does not support unrecognized files, and the file is unrecognized")
            };

        var anime = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
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

        RelocationEventArgs args;
        var renamerInterface = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>));
        if (renamerInterface != null)
        {
            var settingsType = renamerInterface.GetGenericArguments()[0];
            if (settingsType != renamerConfig.Settings.GetType())
                return new Shoko.Plugin.Abstractions.Events.RelocationResult
                {
                    Error = new RelocationError(
                        $"Configured renamer has settings of type {settingsType} but the renamer config has settings of type {renamerConfig.Settings.GetType()}")
                };

            var argsType = typeof(RelocationEventArgs<>).MakeGenericType(settingsType);
            args = (RelocationEventArgs)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, argsType);
            args.Series = anime;
            args.File = place;
            args.Episodes = episodes;
            args.Groups = groups;
            args.AvailableFolders = availableFolders;
            args.MoveEnabled = shouldMove;
            args.RenameEnabled = shouldRename;

            // cached reflection
            if (!_settingsSetters.TryGetValue(argsType, out var settingsSetter))
                _settingsSetters.TryAdd(argsType,
                    settingsSetter = argsType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(a => a.Name == "Settings")?.SetMethod);
            if (settingsSetter == null) return new Shoko.Plugin.Abstractions.Events.RelocationResult {Error = new RelocationError($"Cannot find Settings setter on {renamerInterface}")};
            settingsSetter.Invoke(args, [renamerConfig.Settings]);

            if (!_genericGetNewPaths.TryGetValue(renamerInterface, out var method))
                _genericGetNewPaths.TryAdd(renamerInterface,
                    method = renamerInterface.GetMethod(nameof(IRenamer.GetNewPath), BindingFlags.Instance | BindingFlags.Public));

            if (method == null) return new Shoko.Plugin.Abstractions.Events.RelocationResult {Error = new RelocationError("Cannot find GetNewPath method")};

            return GetNewPath((r, a) => (Shoko.Plugin.Abstractions.Events.RelocationResult)method.Invoke(r, [a])!, renamer, args, shouldRename, shouldMove);
        }

        args = new RelocationEventArgs
        {
            Series = anime,
            File = place,
            Episodes = episodes,
            Groups = groups,
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
    private static Shoko.Plugin.Abstractions.Events.RelocationResult GetNewPath(Func<IBaseRenamer, RelocationEventArgs, Shoko.Plugin.Abstractions.Events.RelocationResult> func, IBaseRenamer renamer, RelocationEventArgs args, bool shouldRename,
        bool shouldMove)
    {
        try
        {
            // get filename from plugin
            var res = func(renamer, args);
            if (res.Error != null) return res;

            // if the plugin said to cancel, then do so
            if (args.Cancel)
                return new Shoko.Plugin.Abstractions.Events.RelocationResult
                {
                    Error = new RelocationError($"Operation canceled by renamer {renamer.GetType().Name}.")
                };

            // TODO check fallback renamer 
            if (shouldRename && string.IsNullOrEmpty(res.FileName))
                return new Shoko.Plugin.Abstractions.Events.RelocationResult
                {
                    Error = new RelocationError($"Set to rename, but renamer {renamer.GetType().Name} did not return a new file name")
                };

            if (shouldMove && (string.IsNullOrEmpty(res.Path) || res.DestinationImportFolder == null))
                return new Shoko.Plugin.Abstractions.Events.RelocationResult
                {
                    Error = new RelocationError($"Renamer {renamer.GetType().Name} did not return a file path")
                };

            return res;
        }
        catch (Exception e)
        {
            return new Shoko.Plugin.Abstractions.Events.RelocationResult
            {
                Error = new RelocationError(e.Message, e)
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
            .Where(a => a.IsClass && a is { IsAbstract: false, IsGenericType: false } && a.GetInterfaces().Any(b =>
                b.IsGenericType && b.GetGenericTypeDefinition() == typeof(IRenamer<>) || b == typeof(IRenamer)))
            .ToList();

        var enabledSetting = _settingsProvider.GetSettings().Plugins.Renamer.EnabledRenamers;
        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerIDAttribute>();
            if (!attributes.Any())
                _logger.LogWarning($"Warning {implementation.Name} has no RenamerIDAttribute and cannot be loaded");
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
