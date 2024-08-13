using System;
using System.Collections.Generic;
using System.IO;
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

using AbstractRelocationResult = Shoko.Plugin.Abstractions.Events.RelocationResult;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Renamer;

public class RenameFileService
{
    private readonly ILogger<RenameFileService> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly RenamerConfigRepository _renamers;
    private readonly Dictionary<Type, MethodInfo?> _settingsSetters = [];
    private readonly Dictionary<Type, MethodInfo?> _genericGetNewPaths = [];

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

    public RelocationResult GetNewPath(SVR_VideoLocal_Place place, RenamerConfig? renamerConfig = null, bool? move = null, bool? rename = null)
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
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Not enough data to do renaming for the recognized file. Missing metadata for {xrefs.Count - episodes.Count} episodes. Aborting.",
            };

        if (renamerConfig == null)
        {
            var defaultRenamerName = settings.Plugins.Renamer.DefaultRenamer;
            if (string.IsNullOrWhiteSpace(defaultRenamerName))
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "No default renamer configured and no renamer config given",
                };

            var defaultRenamer = _renamers.GetByName(defaultRenamerName);
            if (defaultRenamer == null)
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The specified default renamer does not exist",
                };

            renamerConfig = defaultRenamer;
        }

        if (!RenamersByType.TryGetValue(renamerConfig.Type, out var renamer))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"No renamers configured for {renamerConfig.Type}",
            };

        // Check if it's unrecognized.
        if (xrefs.Count == 0 && !renamer.GetType().GetInterfaces().Any(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IUnrecognizedRenamer<>)))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Configured renamer does not support unrecognized files, and the file is unrecognized",
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
        if (renamerInterface is not null)
        {
            var settingsType = renamerInterface.GetGenericArguments()[0];
            if (settingsType != renamerConfig.Settings.GetType())
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = $"Configured renamer has settings of type {settingsType} but the renamer config has settings of type {renamerConfig.Settings.GetType()}",
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

            // Cached reflection.
            if (!_settingsSetters.TryGetValue(argsType, out var settingsSetter))
                _settingsSetters.TryAdd(argsType,
                    settingsSetter = argsType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(a => a.Name == "Settings")?.SetMethod);
            if (settingsSetter == null)
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = $"Cannot find Settings setter on {renamerInterface}",
                };
            settingsSetter.Invoke(args, [renamerConfig.Settings]);

            if (!_genericGetNewPaths.TryGetValue(renamerInterface, out var method))
                _genericGetNewPaths.TryAdd(renamerInterface,
                    method = renamerInterface.GetMethod(nameof(IRenamer.GetNewPath), BindingFlags.Instance | BindingFlags.Public));

            if (method == null)
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "Cannot find GetNewPath method",
                };

            return UnAbstractResult(place, GetNewPath((r, a) => (AbstractRelocationResult)method.Invoke(r, [a])!, renamer, args, shouldRename, shouldMove), shouldMove, shouldRename);
        }

        args = new RelocationEventArgs
        {
            Series = anime,
            File = place,
            Episodes = episodes,
            Groups = groups,
            AvailableFolders = availableFolders,
            MoveEnabled = shouldMove,
            RenameEnabled = shouldRename,
        };

        return UnAbstractResult(place, GetNewPath((r, a) => ((IRenamer)r).GetNewPath(a), renamer, args, shouldRename, shouldMove), shouldMove, shouldRename);
    }

    /// <summary>
    /// Un-abstract the relocation result returned from the renamer, and convert it to something easier to work internally for us.
    /// </summary>
    /// <param name="place">Video file location.</param>
    /// <param name="result">Abstract result returned from the renamed.</param>
    /// <param name="shouldMove">Indicates that we should have moved.</param>
    /// <param name="shouldRename">Indicates that we should have renamed.</param>
    /// <returns>An non-abstract relocation result.</returns>
    private static RelocationResult UnAbstractResult(SVR_VideoLocal_Place place, AbstractRelocationResult result, bool shouldMove, bool shouldRename)
    {
        if (result.Error is not null)
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = result.Error.Message,
                Exception = result.Error.Exception,
            };

        var newImportFolder = shouldMove && !result.SkipMove ? result.DestinationImportFolder! : place.ImportFolder;
        var newFileName = shouldRename && !result.SkipRename ? result.FileName! : place.FileName;
        var newRelativeDirectory = shouldMove && !result.SkipMove ? result.Path! : Path.GetDirectoryName(place.FilePath)!;
        var newRelativePath = newRelativeDirectory.Length > 0 ? Path.Combine(newRelativeDirectory, newFileName) : newFileName;
        var newFullPath = Path.Combine(newImportFolder.Path, newRelativePath);
        return new()
        {
            Success = true,
            ImportFolder = newImportFolder,
            RelativePath = newRelativePath,
            // TODO: Handle file-systems that are or aren't case sensitive.
            Renamed = !string.Equals(place.FileName, result.FileName, StringComparison.OrdinalIgnoreCase),
            Moved = !string.Equals(Path.GetDirectoryName(place.FullServerPath), Path.GetDirectoryName(newFullPath), StringComparison.OrdinalIgnoreCase),
        };
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
    private AbstractRelocationResult GetNewPath(Func<IBaseRenamer, RelocationEventArgs, AbstractRelocationResult> func, IBaseRenamer renamer, RelocationEventArgs args, bool shouldRename,
        bool shouldMove)
    {
        try
        {
            // get filename from plugin
            var result = func(renamer, args);
            if (result.Error is not null) return result;

            // if the plugin said to cancel, then do so
            if (args.Cancel)
                return new AbstractRelocationResult
                {
                    Error = new RelocationError($"Operation canceled by renamer {renamer.GetType().Name}.")
                };

            if (shouldRename && !result.SkipRename && (string.IsNullOrWhiteSpace(result.FileName) || result.FileName.StartsWith("*Error:")))
            {
                var errorMessage = !string.IsNullOrWhiteSpace(result.FileName)
                    ? result.FileName[7..].Trim()
                    : $"The renamer \"{renamer.GetType().Name}\" returned a null or empty value for the file name.";
                _logger.LogError("An error occurred while trying to find a new file name for {FilePath}: {ErrorMessage}", args.File.Path, errorMessage);
                return new() { Error = new RelocationError(errorMessage) };
            }

            // Normalize file name.
            if (!string.IsNullOrEmpty(result.FileName))
            {
                // Move path from file name to path if it's provided in the file name and not as the Path.
                if (Path.GetDirectoryName(result.FileName) is { } dirName && string.IsNullOrEmpty(result.Path))
                    result.Path = dirName;

                // Ensure file name only contains a name and no path.
                result.FileName = Path.GetFileName(result.FileName).Trim();
            }

            // Replace alt separator with main separator.
            result.Path = result.Path?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();

            // Ensure the path does not have a leading separator.
            if (!string.IsNullOrEmpty(result.Path) && result.Path[0] == Path.DirectorySeparatorChar)
                result.Path = result.Path[1..];

            if (shouldMove && !result.SkipMove && (result.DestinationImportFolder is null || result.Path is null || result.Path.StartsWith("*Error:")))
            {
                var errorMessage = !string.IsNullOrWhiteSpace(result.Path)
                    ? result.Path[7..].Trim()
                    : $"The renamer \"{renamer.GetType().Name}\" could not find a valid destination.";
                _logger.LogWarning("An error occurred while trying to find a destination for {FilePath}: {ErrorMessage}", args.File.Path, errorMessage);
                return new() { Error = new RelocationError(errorMessage) };
            }

            return result;
        }
        catch (Exception e)
        {
            return new AbstractRelocationResult
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
                (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(IRenamer<>)) || b == typeof(IRenamer)))
            .ToList();

        var enabledSetting = _settingsProvider.GetSettings().Plugins.Renamer.EnabledRenamers;
        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerIDAttribute>();
            if (!attributes.Any())
                _logger.LogWarning("Warning {ImplementationName} has no RenamerIDAttribute and cannot be loaded", implementation.Name);
            foreach (var id in attributes.Select(a => a.RenamerId))
            {
                var version = implementation.Assembly.GetName().Version;
                if (RenamersByKey.TryGetValue(id, out var value))
                {
                    var info = value.GetType();
                    _logger.LogWarning("{Message}", $"Warning Duplicate renamer ID \"{id}\" of types {implementation}@{implementation.Assembly.Location} (v{version}) and {value}@{info.Assembly.Location} (v{info.Assembly.GetName().Version})");
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
                    _logger.LogInformation("Added Renamer: {Id} (v{Version})", id, version);
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
