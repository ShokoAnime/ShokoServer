using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NHibernate;
using Polly;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Services.Ogg;
using Shoko.Server.Utilities;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Services;

public class VideoLocal_PlaceService
{
    private readonly ILogger<VideoLocal_PlaceService> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly DatabaseFactory _databaseFactory;
    private readonly FileWatcherService _fileWatcherService;
    private readonly RenameFileService _renameFileService;
    private readonly AniDB_FileRepository _aniDBFile;
    private readonly AniDB_EpisodeRepository _aniDBEpisode;
    private readonly CrossRef_File_EpisodeRepository _crossRefFileEpisode;
    private readonly VideoLocalRepository _videoLocal;
    private readonly VideoLocal_PlaceRepository _videoLocalPlace;

    public VideoLocal_PlaceService(ILogger<VideoLocal_PlaceService> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory,
        FileWatcherService fileWatcherService, VideoLocalRepository videoLocal, VideoLocal_PlaceRepository videoLocalPlace,
        CrossRef_File_EpisodeRepository crossRefFileEpisode, AniDB_FileRepository aniDBFile, AniDB_EpisodeRepository aniDBEpisode,
        DatabaseFactory databaseFactory, RenameFileService renameFileService)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _fileWatcherService = fileWatcherService;
        _videoLocal = videoLocal;
        _videoLocalPlace = videoLocalPlace;
        _crossRefFileEpisode = crossRefFileEpisode;
        _aniDBFile = aniDBFile;
        _aniDBEpisode = aniDBEpisode;
        _databaseFactory = databaseFactory;
        _renameFileService = renameFileService;
    }

    #region Relocation (Move & Rename)

    private enum DelayInUse
    {
        First = 750,
        Second = 3000,
        Third = 5000
    }

    #region Methods

    /// <summary>
    /// Relocates a file directly to the specified location based on the given
    /// request.
    /// </summary>
    /// <param name="place">The <see cref="SVR_VideoLocal_Place"/> to relocate.</param>
    /// <param name="request">The <see cref="DirectRelocateRequest"/> containing
    /// the details for the relocation operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public async Task<RelocationResult> DirectlyRelocateFile(SVR_VideoLocal_Place place, DirectRelocateRequest request)
    {
        if (request.ImportFolder is null || string.IsNullOrWhiteSpace(request.RelativePath))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Invalid request object, import folder, or relative path.",
            };

        if (place.VideoLocal is not { } video)
        {
            _logger.LogWarning("Could not find the associated video for the file location: {LocationID}", place.VideoLocal_Place_ID);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find the associated video for the file location: {place.VideoLocal_Place_ID}",
            };
        }

        // Sanitize relative path and reject paths leading to outside the import folder.
        var fullPath = Path.GetFullPath(Path.Combine(request.ImportFolder.Path, request.RelativePath));
        if (!fullPath.StartsWith(request.ImportFolder.Path, StringComparison.OrdinalIgnoreCase))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "The provided relative path leads outside the import folder.",
            };

        var oldRelativePath = place.FilePath;
        var oldFullPath = place.FullServerPath;
        if (string.IsNullOrWhiteSpace(oldRelativePath) || string.IsNullOrWhiteSpace(oldFullPath))
        {
            _logger.LogWarning("Could not find or access the file to move: {LocationID}", place.VideoLocal_Place_ID);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find or access the file to move: {place.VideoLocal_Place_ID}",
            };
        }

        // this can happen due to file locks, so retry in a while.
        if (!File.Exists(oldFullPath))
        {
            _logger.LogWarning("Could not find or access the file to move: {FilePath}", oldFullPath);
            return new()
            {
                Success = false,
                ShouldRetry = true,
                ErrorMessage = $"Could not find or access the file to move: \"{oldFullPath}\"",
            };
        }

        var dropFolder = (IImportFolder?)place.ImportFolder;
        if (dropFolder is null)
        {
            _logger.LogTrace("Unable to find import folder for file with ID {VideoLocal}", place.VideoLocal);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Unable to find import folder for file with ID {place.ImportFolderID}.",
            };
        }

        // Don't relocate files not in a drop source or drop destination.
        if (dropFolder.DropFolderType is DropFolderType.Excluded)
        {
            _logger.LogTrace("Not relocating file as it is not in a drop source or drop destination: {FullPath}", oldFullPath);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Not relocating file as it is not in a drop source or drop destination: \"{oldFullPath}\"",
            };
        }

        // Or if it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled.
        if (dropFolder.DropFolderType is DropFolderType.Destination && !request.AllowRelocationInsideDestination)
        {
            _logger.LogTrace("Not relocating file because it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled: {FullPath}", oldFullPath);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Not relocating file because it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled: \"{oldFullPath}\"",
            };
        }

        // Check if the import folder can accept the file.
        var settings = _settingsProvider.GetSettings();
        var relocationService = Utils.ServiceContainer.GetRequiredService<IRelocationService>();
        if (!settings.Import.SkipDiskSpaceChecks && !relocationService.ImportFolderHasSpace(request.ImportFolder, place))
        {
            _logger.LogWarning("The import folder cannot accept the file due to too little space available: {FilePath}", oldFullPath);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"The import folder cannot accept the file due to too little space available: \"{oldFullPath}\"",
            };
        }

        var newRelativePath = Path.GetRelativePath(request.ImportFolder.Path, fullPath);
        var newFolderPath = Path.GetDirectoryName(newRelativePath);
        var newFullPath = Path.Combine(request.ImportFolder.Path, newRelativePath);
        var newFileName = Path.GetFileName(newRelativePath);
        var renamed = !string.Equals(Path.GetFileName(oldRelativePath), newFileName, StringComparison.OrdinalIgnoreCase);
        var moved = !string.Equals(Path.GetDirectoryName(oldFullPath), Path.GetDirectoryName(newFullPath), StringComparison.OrdinalIgnoreCase);

        // Last ditch effort to ensure we aren't moving a file unto itself
        if (string.Equals(newFullPath, oldFullPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace("Resolved to relocate {FilePath} onto itself. Nothing to do.", newFullPath);
            return new()
            {
                Success = true,
                ImportFolder = request.ImportFolder,
                RelativePath = newRelativePath,
            };
        }

        var destFullTree = string.IsNullOrEmpty(newFolderPath) ? request.ImportFolder.Path : Path.Combine(request.ImportFolder.Path, newFolderPath);
        if (!Directory.Exists(destFullTree))
        {
            _fileWatcherService.AddFileWatcherExclusion(destFullTree);
            try
            {
                Directory.CreateDirectory(destFullTree);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while trying to create the new destination tree for {FilePath}\n{ErrorMessage}", newFullPath, ex.Message);
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = ex.Message,
                };
            }
            finally
            {
                _fileWatcherService.RemoveFileWatcherExclusion(destFullTree);
            }
        }

        var sourceFile = new FileInfo(oldFullPath);
        var destVideoLocalPlace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(newRelativePath, request.ImportFolder.ID);
        if (File.Exists(newFullPath))
        {
            // A file with the same name exists at the destination.
            _logger.LogTrace("A file already exists at the new location, checking it for duplicate…");
            var destVideoLocal = destVideoLocalPlace?.VideoLocal;
            if (destVideoLocalPlace is null || destVideoLocal is null)
            {
                _logger.LogWarning("The existing file at the new location does not have a VideoLocal. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The existing file at the new location does not have a VideoLocal. Not moving.",
                };
            }

            if (destVideoLocal.Hash == video.Hash)
            {
                _logger.LogDebug("Not moving file as it already exists at the new location, deleting source file instead: {PreviousPath} to {NextPath}", oldFullPath, newFullPath);

                // if the file already exists, we can just delete the source file instead
                // this is safer than deleting and moving
                try
                {
                    sourceFile.Delete();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Unable to DELETE file: {FilePath}\n{ErrorMessage}", place.FullServerPath, e.Message);
                    await RemoveRecord(place, false);

                    if (request.DeleteEmptyDirectories)
                        RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(oldFullPath), dropFolder.Path);
                    return new()
                    {
                        Success = false,
                        ShouldRetry = false,
                        ErrorMessage = $"Unable to DELETE file: \"{place.FullServerPath}\" error {e}",
                    };
                }
            }

            // Not a dupe, don't delete it
            _logger.LogTrace("A file already exists at the new location, checking it for version and group");
            var destinationExistingAniDBFile = destVideoLocal.AniDBFile;
            if (destinationExistingAniDBFile is null)
            {
                _logger.LogWarning("The existing file at the new location does not have AniDB info. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The existing file at the new location does not have AniDB info. Not moving.",
                };
            }

            var aniDBFile = video.AniDBFile;
            if (aniDBFile is null)
            {
                _logger.LogWarning("The file does not have AniDB info. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The file does not have AniDB info. Not moving.",
                };
            }

            if (destinationExistingAniDBFile.GroupID == aniDBFile.GroupID &&
                destinationExistingAniDBFile.FileVersion < aniDBFile.FileVersion)
            {
                // This is a V2 replacing a V1 with the same name.
                // Normally we'd let the Multiple Files Utility handle it, but let's just delete the V1
                _logger.LogInformation("The existing file is a V1 from the same group. Replacing it.");

                // Delete the destination
                await RemoveRecordAndDeletePhysicalFile(destVideoLocalPlace);

                // Move
                _fileWatcherService.AddFileWatcherExclusion(oldFullPath);
                _fileWatcherService.AddFileWatcherExclusion(newFullPath);
                _logger.LogInformation("Moving file from {PreviousPath} to {NextPath}", oldFullPath, newFullPath);
                try
                {
                    sourceFile.MoveTo(newFullPath);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to MOVE file: {PreviousPath} to {NextPath}\n{ErrorMessage}", oldFullPath, newFullPath, e.Message);
                    return new()
                    {
                        Success = false,
                        ShouldRetry = true,
                        ErrorMessage = $"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}",
                    };
                }
                finally
                {
                    _fileWatcherService.RemoveFileWatcherExclusion(oldFullPath);
                    _fileWatcherService.RemoveFileWatcherExclusion(newFullPath);
                }

                place.ImportFolderID = request.ImportFolder.ID;
                place.FilePath = newRelativePath;
                RepoFactory.VideoLocalPlace.Save(place);

                if (request.DeleteEmptyDirectories)
                {
                    // For some reason this totally hangs, if the Folder is a network folder, and multiple thread are doing it.
                    // IDK why, Shoko get totally frozen, but it seems a .NET issue.
                    // https://stackoverflow.com/questions/33036650/directory-enumeratedirectories-hang-on-some-network-folders
                    /*
                    var directories = dropFolder.BaseDirectory.EnumerateDirectories("*", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true })
                   .Select(dirInfo => dirInfo.FullName);
                    RecursiveDeleteEmptyDirectories(directories, dropFolder.ImportFolderLocation);
                    */
                    RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(oldFullPath), dropFolder.Path);
                }
            }
        }
        else
        {
            if (destVideoLocalPlace is not null)
            {
                _logger.LogTrace("An entry already exists for the new location at {NewPath} but no physical file resides there. Removing the entry.", newFullPath);
                await RemoveRecord(destVideoLocalPlace);
            }

            // Move
            _fileWatcherService.AddFileWatcherExclusion(oldFullPath);
            _fileWatcherService.AddFileWatcherExclusion(newFullPath);
            _logger.LogInformation("Moving file from {PreviousPath} to {NextPath}", oldFullPath, newFullPath);
            try
            {
                sourceFile.MoveTo(newFullPath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to MOVE file: {PreviousPath} to {NextPath}\n{ErrorMessage}", oldFullPath, newFullPath, e.Message);
                return new()
                {
                    Success = false,
                    ShouldRetry = true,
                    ErrorMessage = $"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}",
                };
            }
            finally
            {
                _fileWatcherService.RemoveFileWatcherExclusion(oldFullPath);
                _fileWatcherService.RemoveFileWatcherExclusion(newFullPath);
            }

            place.ImportFolderID = request.ImportFolder.ID;
            place.FilePath = newRelativePath;
            RepoFactory.VideoLocalPlace.Save(place);

            if (request.DeleteEmptyDirectories)
            {
                // For some reason this totally hangs, if the Folder is a network folder, and multiple thread are doing it.
                // IDK why, Shoko get totally frozen, but it seems a .NET issue.
                // https://stackoverflow.com/questions/33036650/directory-enumeratedirectories-hang-on-some-network-folders
                /*
                var directories = dropFolder.BaseDirectory.EnumerateDirectories("*", new EnumerationOptions() { RecurseSubdirectories = true, IgnoreInaccessible = true })
                    .Select(dirInfo => dirInfo.FullName);
                RecursiveDeleteEmptyDirectories(directories, dropFolder.ImportFolderLocation);
                */
                RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(oldFullPath), dropFolder.Path);
            }
        }

        if (renamed)
        {
            // Add a new or update an existing lookup entry.
            var existingEntries = RepoFactory.FileNameHash.GetByHash(video.Hash);
            if (!existingEntries.Any(a => a.FileName.Equals(newFileName)))
            {
                var hash = RepoFactory.FileNameHash.GetByFileNameAndSize(newFileName, video.FileSize).FirstOrDefault() ??
                    new() { FileName = newFileName, FileSize = video.FileSize };
                hash.DateTimeUpdated = DateTime.Now;
                hash.Hash = video.Hash;
                RepoFactory.FileNameHash.Save(hash);
            }
        }

        // Move the external subtitles.
        MoveExternalSubtitles(newFullPath, oldFullPath);

        // Fire off the moved/renamed event depending on what was done.
        if (renamed && !moved)
            ShokoEventHandler.Instance.OnFileRenamed(request.ImportFolder, Path.GetFileName(oldRelativePath), newFileName, place);
        else
            ShokoEventHandler.Instance.OnFileMoved(dropFolder, request.ImportFolder, oldRelativePath, newRelativePath, place);

        return new()
        {
            Success = true,
            ShouldRetry = false,
            ImportFolder = request.ImportFolder,
            RelativePath = newRelativePath,
            Moved = moved,
            Renamed = renamed,
        };
    }

    /// <summary>
    /// Automatically relocates a file using the specified relocation request or
    /// default settings.
    /// </summary>
    /// <param name="place">The <see cref="SVR_VideoLocal_Place"/> to relocate.</param>
    /// <param name="request">The <see cref="AutoRelocateRequest"/> containing
    /// the details for the relocation operation, or null for default settings.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public async Task<RelocationResult> AutoRelocateFile(SVR_VideoLocal_Place place, AutoRelocateRequest? request = null)
    {
        // Allows calling the method without any parameters.
        var settings = _settingsProvider.GetSettings();
        // give defaults from the settings
        request ??= new()
        {
            Move = settings.Plugins.Renamer.MoveOnImport,
            Rename = settings.Plugins.Renamer.RenameOnImport,
            DeleteEmptyDirectories = settings.Plugins.Renamer.MoveOnImport,
            AllowRelocationInsideDestination = settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport,
            Renamer = RepoFactory.RenamerConfig.GetByName(settings.Plugins.Renamer.DefaultRenamer),
        };

        if (request is { Preview: true, Renamer: null })
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Cannot preview without a renamer given",
            };

        if (request is { Move: false, Rename: false })
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Rename and Move are both set to false. Nothing to do.",
            };

        // make sure we can find the file
        var previousLocation = place.FullServerPath;
        if (!File.Exists(place.FullServerPath))
        {
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find or access the file to move: {place.FileName} ({place.VideoLocal_Place_ID})"
            };
        }

        var retryPolicy = Policy
            .HandleResult<RelocationResult>(a => a.ShouldRetry)
            .Or<Exception>(e =>
            {
                _logger.LogError(e, "Error Renaming/Moving File");
                return false;
            })
            .WaitAndRetryAsync([
                TimeSpan.FromMilliseconds((int)DelayInUse.First),
                TimeSpan.FromMilliseconds((int)DelayInUse.Second),
                TimeSpan.FromMilliseconds((int)DelayInUse.Third),
            ]);

        var relocationResult = await retryPolicy.ExecuteAsync(() => RelocateFile(place, request)).ConfigureAwait(false);
        if (!relocationResult.Success)
            return relocationResult;

        // Set the linux permissions now if we're not previewing the result.
        if (!request.Preview)
        {
            try
            {
                LinuxFS.SetLinuxPermissions(place.FullServerPath, settings.Linux.UID, settings.Linux.GID, settings.Linux.Permission);
            }
            catch (InvalidOperationException e)
            {
                _logger.LogError(e, "Unable to set permissions ({Uid}:{Gid} {Permission}) on file {FileName}: Access Denied", settings.Linux.UID,
                    settings.Linux.GID, settings.Linux.Permission, place.FileName);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error setting Linux Permissions: {Ex}", e);
            }
        }

        var correctFileName = Path.GetFileName(relocationResult.RelativePath);
        var correctFolder = Path.GetDirectoryName(relocationResult.RelativePath);
        var correctRelativePath = !string.IsNullOrEmpty(correctFolder) ? Path.Combine(correctFolder, correctFileName) : correctFileName;
        var correctFullPath = Path.Combine(relocationResult.ImportFolder!.Path, correctRelativePath);
        if (request.Preview)
            _logger.LogTrace("Resolved to move from {PreviousPath} to {NextPath}.", previousLocation, correctFullPath);
        else
            _logger.LogTrace("Moved from {PreviousPath} to {NextPath}.", previousLocation, correctFullPath);
        return new()
        {
            Success = true,
            ShouldRetry = false,
            ImportFolder = relocationResult.ImportFolder,
            RelativePath = correctRelativePath,
            Moved = relocationResult.Moved,
            Renamed = relocationResult.Renamed,
        };
    }

    /// <summary>
    /// Renames a file using the specified rename request.
    /// </summary>
    /// <param name="place">The <see cref="SVR_VideoLocal_Place"/> to rename.</param>
    /// <param name="request">The <see cref="AutoRelocateRequest"/> containing the
    ///     details for the rename operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the rename operation.</returns>
    public async Task<RelocationResult> RelocateFile(SVR_VideoLocal_Place place, AutoRelocateRequest request)
    {
        // Just return the existing values if we're going to skip the operation.
        if (request is { Rename: false, Move: false })
            return new()
            {
                Success = true,
                ShouldRetry = false,
                ImportFolder = place.ImportFolder,
                RelativePath = place.FilePath,
            };

        RelocationResult result;
        // run the renamer and process the result
        try
        {
            result = _renameFileService.GetNewPath(place, request.Renamer, request.Move, request.Rename, request.AllowRelocationInsideDestination);
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;

            _logger.LogError(ex, "An error occurred while trying to find a new file name for {FilePath}: {ErrorMessage}", place.FullServerPath, errorMessage);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = errorMessage,
                Exception = ex,
            };
        }

        // Return early if we're only previewing or if it not a success.
        if (request.Preview || !result.Success)
            return result;

        // Actually move it.
        return await DirectlyRelocateFile(place, new()
        {
            DeleteEmptyDirectories = request.DeleteEmptyDirectories,
            AllowRelocationInsideDestination = request.AllowRelocationInsideDestination,
            ImportFolder = result.ImportFolder,
            RelativePath = result.RelativePath,
        });
    }

    #endregion Methods
    #region Helpers

    private void MoveExternalSubtitles(string newFullServerPath, string oldFullServerPath)
    {
        try
        {
            var oldParent = Path.GetDirectoryName(oldFullServerPath);
            var newParent = Path.GetDirectoryName(newFullServerPath);
            var oldFileName = Path.GetFileNameWithoutExtension(oldFullServerPath);
            var newFileName = Path.GetFileNameWithoutExtension(newFullServerPath);
            if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(oldParent) ||
                string.IsNullOrEmpty(oldFileName) || string.IsNullOrEmpty(newFileName))
                return;

            var textStreams = SubtitleHelper.GetSubtitleStreams(oldFullServerPath);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename))
                    continue;

                var subPath = Path.Combine(oldParent, subtitleFile.Filename);
                var subFile = new FileInfo(subPath);
                if (!subFile.Exists)
                {
                    _logger.LogError("Unable to rename external subtitle file {SubtitleFile}. Cannot access the file.", subPath);
                    continue;
                }

                var newSubPath = Path.Combine(newParent, newFileName + subtitleFile.Filename[oldFileName.Length..]);
                if (string.Equals(subPath, newSubPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Attempting to move subtitle file onto itself. Skipping. Path: {FilePath} to {FilePath}", subPath, newSubPath);
                    continue;
                }

                if (File.Exists(newSubPath))
                {
                    try
                    {
                        File.Delete(newSubPath);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Unable to DELETE file: {SubtitleFile}\n{ErrorMessage}", subPath, e.Message);
                    }
                }

                try
                {
                    subFile.MoveTo(newSubPath);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Unable to MOVE file: {PreviousFile} to {NextPath}\n{ErrorMessage}", subPath, newSubPath, e.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while trying to move an external subtitle file for {FilePath}\n{ErrorMessage}", oldFullServerPath, ex.Message);
        }
    }

    private void RecursiveDeleteEmptyDirectories(string? toBeChecked, string? directoryToClean)
        => RecursiveDeleteEmptyDirectories([toBeChecked], directoryToClean);

    private void RecursiveDeleteEmptyDirectories(IEnumerable<string?> toBeChecked, string? directoryToClean)
    {
        if (string.IsNullOrEmpty(directoryToClean))
            return;
        try
        {
            directoryToClean = directoryToClean.TrimEnd(Path.DirectorySeparatorChar);
            var directoriesToClean = toBeChecked
                .SelectMany(path =>
                {
                    int? isExcludedAt = null;
                    var paths = new List<(string path, int level)>();
                    while (!string.IsNullOrEmpty(path))
                    {
                        var level = path == directoryToClean ? 0 : path[(directoryToClean.Length + 1)..].Split(Path.DirectorySeparatorChar).Length;
                        if (path == directoryToClean)
                            break;
                        if (_settingsProvider.GetSettings().Import.Exclude.Any(reg => Regex.IsMatch(path, reg)))
                            isExcludedAt = level;
                        paths.Add((path, level));
                        path = Path.GetDirectoryName(path);
                    }
                    return isExcludedAt.HasValue
                        ? paths.Where(tuple => tuple.level < isExcludedAt.Value)
                        : paths;
                })
                .DistinctBy(tuple => tuple.path)
                .OrderByDescending(tuple => tuple.level)
                .ThenBy(tuple => tuple.path)
                .Select(tuple => tuple.path)
                .ToList();
            foreach (var directoryPath in directoriesToClean)
            {
                if (Directory.Exists(directoryPath) && IsDirectoryEmpty(directoryPath))
                {
                    _logger.LogTrace("Removing EMPTY directory at {Path}", directoryPath);

                    try
                    {
                        Directory.Delete(directoryPath);
                    }
                    catch (Exception ex)
                    {
                        if (ex is DirectoryNotFoundException or FileNotFoundException) return;
                        _logger.LogWarning(ex, "Unable to DELETE directory: {Directory}", directoryPath);
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException) return;
            _logger.LogError(e, "There was an error removing the empty directories in {Dir}\n{Ex}", directoryToClean, e);
        }
    }

    private static bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    #endregion Helpers
    #endregion Relocation (Move & Rename)

    double CalculateDurationOggFile(string filename)
    {
        try
        {
            var oggFile = OggFile.ParseFile(filename);
            return oggFile.Duration;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to parse duration from Ogg-Vorbis file {filename}.", filename);
            return 0;
        }
    }

    public bool RefreshMediaInfo(SVR_VideoLocal_Place place)
    {
        try
        {
            _logger.LogTrace("Getting media info for: {Place}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
            MediaContainer? m = null;
            if (place.VideoLocal is null)
            {
                _logger.LogError("VideoLocal for {Place} failed to be retrieved for MediaInfo", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
                return false;
            }

            if (!string.IsNullOrEmpty(place.FullServerPath))
            {
                if (place.GetFile() is null)
                {
                    _logger.LogError("File {Place} failed to be retrieved for MediaInfo", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
                    return false;
                }

                var name = place.FullServerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
                m = Utilities.MediaInfoLib.MediaInfo.GetMediaInfo(name); // MediaInfo should have libcurl.dll for http

                if (m?.GeneralStream != null && m.GeneralStream.Duration == 0 && m.GeneralStream.Format != null && m.GeneralStream.Format.Equals("ogg", StringComparison.InvariantCultureIgnoreCase))
                {
                    m.GeneralStream.Duration = CalculateDurationOggFile(name);
                }

                var duration = m?.GeneralStream?.Duration ?? 0;
                if (duration == 0)
                {
                    m = null;
                }
            }

            if (m is not null)
            {
                var info = place.VideoLocal;

                var subs = SubtitleHelper.GetSubtitleStreams(place.FullServerPath);
                if (subs.Count > 0)
                {
                    m.media?.track.AddRange(subs);
                }

                info.MediaInfo = m;
                info.MediaVersion = SVR_VideoLocal.MEDIA_VERSION;
                return true;
            }

            _logger.LogError("File {Place} failed to read MediaInfo", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to read the media information of file {Place} ERROR: {Ex}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString(),
                e);
        }

        return false;
    }

    public async Task RemoveRecordAndDeletePhysicalFile(SVR_VideoLocal_Place place, bool deleteFolder = true)
    {
        _logger.LogInformation("Deleting video local place record and file: {Place}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());

        if (!File.Exists(place.FullServerPath))
        {
            _logger.LogInformation("Unable to find file. Removing Record: {Place}", place.FullServerPath ?? place.FilePath);
            await RemoveRecord(place);
            return;
        }

        try
        {
            File.Delete(place.FullServerPath);
            DeleteExternalSubtitles(place.FullServerPath);
        }
        catch (FileNotFoundException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", place.FullServerPath, ex);
            throw;
        }

        if (deleteFolder)
            RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.FullServerPath), place.ImportFolder!.ImportFolderLocation);

        await RemoveRecord(place);
    }

    public async Task RemoveAndDeleteFileWithOpenTransaction(ISession session, SVR_VideoLocal_Place place, HashSet<SVR_AnimeSeries> seriesToUpdate, bool updateMyList = true, bool deleteFolders = true)
    {
        try
        {
            _logger.LogInformation("Deleting video local place record and file: {Place}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());

            if (!File.Exists(place.FullServerPath))
            {
                _logger.LogInformation("Unable to find file. Removing Record: {FullServerPath}", place.FullServerPath);
                await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, updateMyList);
                return;
            }

            try
            {
                File.Delete(place.FullServerPath);
                DeleteExternalSubtitles(place.FullServerPath);
            }
            catch (FileNotFoundException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", place.FullServerPath, ex);
                return;
            }

            if (deleteFolders) RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.FullServerPath), place.ImportFolder!.ImportFolderLocation);
            await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, updateMyList);
            // For deletion of files from Trakt, we will rely on the Daily sync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not delete file and remove record for \"{Place}\": {Ex}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString(),
                ex);
        }
    }

    private void DeleteExternalSubtitles(string originalFileName)
    {
        try
        {
            var textStreams = SubtitleHelper.GetSubtitleStreams(originalFileName);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;

                var srcParent = Path.GetDirectoryName(originalFileName);
                if (string.IsNullOrEmpty(srcParent)) continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath)) continue;

                try
                {
                    File.Delete(subPath);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to delete file: \"{SubtitleFile}\"", subtitleFile.Filename);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error deleting external subtitles: {Ex}", ex);
        }
    }

    public async Task RemoveRecord(SVR_VideoLocal_Place place, bool updateMyListStatus = true)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
        var seriesToUpdate = new List<SVR_AnimeSeries>();
        var v = place.VideoLocal;
        var scheduler = await _schedulerFactory.GetScheduler();

        using (var session = _databaseFactory.SessionFactory.OpenSession())
        {
            if (v?.Places?.Count <= 1)
            {
                if (updateMyListStatus)
                {
                    if (RepoFactory.AniDB_File.GetByHash(v.Hash) is null)
                    {
                        var xrefs = v.EpisodeCrossReferences;
                        foreach (var xref in xrefs)
                        {
                            if (xref.AnimeID is 0)
                                continue;

                            var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                            if (ep is null)
                                continue;

                            await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                                {
                                    c.AnimeID = xref.AnimeID;
                                    c.EpisodeType = ep.GetEpisodeTypeEnum();
                                    c.EpisodeNumber = ep.EpisodeNumber;
                                }
                            );
                        }
                    }
                    else
                    {
                        await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                            {
                                c.Hash = v.Hash;
                                c.FileSize = v.FileSize;
                            }
                        );
                    }
                }

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder!, place, v);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);

                    seriesToUpdate.AddRange(
                        v
                            .AnimeEpisodes
                            .DistinctBy(a => a.AnimeSeriesID)
                            .Select(a => a.AnimeSeries)
                            .WhereNotNull()
                    );
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, v);
                    transaction.Commit();
                });
            }
            else
            {
                if (v is not null)
                {
                    try
                    {
                        ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder!, place, v);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
                    transaction.Commit();
                });
            }
        }

        await Task.WhenAll(seriesToUpdate.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task RemoveRecordWithOpenTransaction(ISession session, SVR_VideoLocal_Place place, ICollection<SVR_AnimeSeries> seriesToUpdate,
        bool updateMyListStatus = true)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
        var v = place.VideoLocal;

        if (v?.Places?.Count <= 1)
        {
            if (updateMyListStatus)
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                if (_aniDBFile.GetByHash(v.Hash) is null)
                {
                    var xrefs = _crossRefFileEpisode.GetByEd2k(v.Hash);
                    foreach (var xref in xrefs)
                    {
                        if (xref.AnimeID is 0)
                            continue;

                        var ep = _aniDBEpisode.GetByEpisodeID(xref.EpisodeID);
                        if (ep is null)
                            continue;

                        await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                        {
                            c.AnimeID = xref.AnimeID;
                            c.EpisodeType = ep.GetEpisodeTypeEnum();
                            c.EpisodeNumber = ep.EpisodeNumber;
                        });
                    }
                }
                else
                {
                    await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                        {
                            c.Hash = v.Hash;
                            c.FileSize = v.FileSize;
                        }
                    );
                }
            }

            var eps = v.AnimeEpisodes?.WhereNotNull().ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.AnimeSeries).WhereNotNull().ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder!, place, v);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlace.DeleteWithOpenTransaction(session, place);
                _videoLocal.DeleteWithOpenTransaction(session, v);
                transaction.Commit();
            });
        }
        else
        {
            if (v is not null)
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder!, place, v);
                }
                catch
                {
                    // ignore
                }
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlace.DeleteWithOpenTransaction(session, place);
                transaction.Commit();
            });
        }
    }
}
