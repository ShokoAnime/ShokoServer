using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NHibernate;
using Polly;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services;

public class VideoLocal_PlaceService
{
    private readonly ILogger<VideoLocal_PlaceService> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;

    public VideoLocal_PlaceService(ILogger<VideoLocal_PlaceService> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
    }

    private enum DelayInUse
    {
        First = 750,
        Second = 3000,
        Third = 5000
    }

    public void RenameAndMoveAsRequired(SVR_VideoLocal_Place place)
    {
        var settings = _settingsProvider.GetSettings();
        var invert = settings.Import.RenameThenMove;

        var retryPolicy = Policy
            .HandleResult<IFileOperationResult>(a => a.CanRetry)
            .WaitAndRetry(new[]
            {
                TimeSpan.FromMilliseconds((int)DelayInUse.First),
                TimeSpan.FromMilliseconds((int)DelayInUse.Second),
                TimeSpan.FromMilliseconds((int)DelayInUse.Third)
            });

        var result = retryPolicy.Execute(() => invert ? RenameIfRequired(place) : MoveFileIfRequired(place));

        // Don't bother renaming if we couldn't move. It'll need user interaction
        if (!result.IsSuccess) return;

        // Retry logic for the second attempt
        result = retryPolicy.Execute(() => invert ? MoveFileIfRequired(place) : RenameIfRequired(place));

        if (!result.IsSuccess) return;

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

    private RenameFileResult RenameIfRequired(SVR_VideoLocal_Place place)
    {
        if (!_settingsProvider.GetSettings().Import.RenameOnImport)
        {
            _logger.LogTrace("Skipping rename of \"{FullServerPath}\" as rename on import is disabled", place.FullServerPath);
            return new RenameFileResult { IsSuccess = true, NewFilename = string.Empty };
        }

        var result = RenameFile(place);

        return result;
    }

    public RenameFileResult RenameFile(SVR_VideoLocal_Place place, bool preview = false, string scriptName = null)
    {
        if (scriptName != null && scriptName.Equals(Shoko.Models.Constants.Renamer.TempFileName))
        {
            return new RenameFileResult { NewFilename = string.Empty, ErrorMessage = "Do not attempt to use a temp file to rename" };
        }

        if (place.ImportFolder == null)
        {
            _logger.LogError("The renamer can\'t get the import folder for ImportFolderID: {ImportFolderID}, File: \"{FilePath}\"",
                place.ImportFolderID, place.FilePath);
            return new RenameFileResult { NewFilename = string.Empty, ErrorMessage = "Could not find the file" };
        }

        string renamed;
        try
        {
            renamed = RenameFileHelper.GetFilename(place, scriptName);
        }
        catch (Exception e)
        {
            return new RenameFileResult { NewFilename = string.Empty, ErrorMessage = e.Message, Exception = e };
        }

        if (string.IsNullOrEmpty(renamed))
        {
            _logger.LogError("The renamer returned a null or empty name for: \"{FullServerPath}\"", place.FullServerPath);
            return new RenameFileResult { NewFilename = string.Empty, ErrorMessage = "The file renamer returned a null or empty value" };
        }

        if (renamed.StartsWith("*Error: "))
        {
            _logger.LogError("The renamer returned an error on file: \"{FullServerPath}\"\n            {Renamed}", place.FullServerPath, renamed);
            return new RenameFileResult { NewFilename = string.Empty, ErrorMessage = renamed[7..] };
        }

        // actually rename the file
        var fullFileName = place.FullServerPath;

        // check if the file exists
        if (string.IsNullOrEmpty(fullFileName))
        {
            _logger.LogError("Could not find the original file for renaming, or it is in use: \"{FileName}\"", fullFileName);
            return new RenameFileResult { CanRetry = true, NewFilename = renamed, ErrorMessage = "Could not access the file" };
        }

        if (!File.Exists(fullFileName))
        {
            _logger.LogError("Error could not find the original file for renaming, or it is in use: \"{FileName}\"", fullFileName);
            return new RenameFileResult { CanRetry = true, NewFilename = renamed, ErrorMessage = "Could not access the file" };
        }

        // actually rename the file
        var path = Path.GetDirectoryName(fullFileName);
        var newFullName = Path.Combine(path!, renamed);

        try
        {
            if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogInformation("Renaming file SKIPPED! no change From \"{FullFileName}\" to \"{NewFullName}\"", fullFileName, newFullName);
                return new RenameFileResult { IsSuccess = true, NewFilename = renamed };
            }

            if (File.Exists(newFullName))
            {
                _logger.LogInformation("Renaming file SKIPPED! Destination Exists \"{NewFullName}\"", newFullName);
                return new RenameFileResult { NewFilename = renamed, ErrorMessage = "The filename already exists" };
            }

            if (preview)
            {
                return new RenameFileResult { IsSuccess = true, NewFilename = renamed };
            }

            Utils.ShokoServer.AddFileWatcherExclusion(newFullName);

            _logger.LogInformation("Renaming file From \"{FullFileName}\" to \"{NewFullName}\"", fullFileName, newFullName);
            try
            {
                var file = new FileInfo(fullFileName);
                file.MoveTo(newFullName);
            }
            catch (Exception e)
            {
                _logger.LogInformation(e, "Renaming file FAILED! From \"{FullFileName}\" to \"{NewFullName}\" - {Ex}", fullFileName, newFullName, e);
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullName);
                return new RenameFileResult { CanRetry = true, NewFilename = renamed, ErrorMessage = e.Message, Exception = e };
            }

            // Rename external subs!
            RenameExternalSubtitles(fullFileName, renamed);

            _logger.LogInformation("Renaming file SUCCESS! From \"{FullFileName}\" to \"{NewFullName}\"", fullFileName, newFullName);
            var (folder, filePath) = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
            if (folder == null)
            {
                _logger.LogError("Unable to LOCATE file \"{NewFullName}\" inside the import folders", newFullName);
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullName);
                return new RenameFileResult { NewFilename = renamed, ErrorMessage = "Unable to resolve new path" };
            }

            // Rename hash xrefs
            var filenameHash = RepoFactory.FileNameHash.GetByHash(place.VideoLocal.Hash);
            if (!filenameHash.Any(a => a.FileName.Equals(renamed)))
            {
                var fnHash = new FileNameHash
                {
                    DateTimeUpdated = DateTime.Now,
                    FileName = renamed,
                    FileSize = place.VideoLocal.FileSize,
                    Hash = place.VideoLocal.Hash
                };
                RepoFactory.FileNameHash.Save(fnHash);
            }

            place.FilePath = filePath;
            RepoFactory.VideoLocalPlace.Save(place);
            // just in case
#pragma warning disable CS0618 // Type or member is obsolete
            place.VideoLocal.FileName = renamed;
#pragma warning restore CS0618 // Type or member is obsolete
            RepoFactory.VideoLocal.Save(place.VideoLocal, false);
            
            ShokoEventHandler.Instance.OnFileRenamed(place.ImportFolder, Path.GetFileName(fullFileName), renamed, place);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Renaming file FAILED! From \"{FullFileName}\" to \"{NewFullName}\" - {ExMessage}", fullFileName, newFullName, ex);
            return new RenameFileResult { CanRetry = true, NewFilename = renamed, ErrorMessage = ex.Message, Exception = ex };
        }

        Utils.ShokoServer.RemoveFileWatcherExclusion(newFullName);
        return new RenameFileResult { IsSuccess = true, NewFilename = renamed };
    }
    
    private void RenameExternalSubtitles(string fullFileName, string renamed)
    {
        var textStreams = SubtitleHelper.GetSubtitleStreams(fullFileName);
        var path = Path.GetDirectoryName(fullFileName);
        var oldBasename = Path.GetFileNameWithoutExtension(fullFileName);
        var newBasename = Path.GetFileNameWithoutExtension(renamed);
        foreach (var sub in textStreams)
        {
            if (string.IsNullOrEmpty(sub.Filename))
            {
                continue;
            }

            var oldSubPath = Path.Combine(path!, sub.Filename);

            if (!File.Exists(oldSubPath))
            {
                _logger.LogError("Unable to rename external subtitle \"{SubFilename}\". Cannot access the file", sub.Filename);
                continue;
            }

            var newSub = Path.Combine(path, sub.Filename.Replace(oldBasename, newBasename));
            try
            {
                var file = new FileInfo(oldSubPath);
                file.MoveTo(newSub);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to rename external subtitle \"{SubFilename}\" to \"{NewSub}\". {Ex}", sub.Filename, newSub, e);
            }
        }
    }

    private MoveFileResult MoveFileIfRequired(SVR_VideoLocal_Place place, bool deleteEmpty = true)
    {
        if (!_settingsProvider.GetSettings().Import.MoveOnImport)
        {
            _logger.LogTrace("Skipping move of \"{FullServerPath}\" as move on import is disabled", place.FullServerPath);
            return new MoveFileResult { IsSuccess = true, NewFolder = string.Empty };
        }

        if (place.ImportFolder.IsDropSource == 0)
        {
            _logger.LogTrace("Not moving file as it is NOT in the drop folder: \"{FullServerPath}\"", place.FullServerPath);
            return new MoveFileResult { IsSuccess = true, NewFolder = string.Empty };
        }

        var result = MoveFile(place, deleteEmpty);

        return result;
    }

    public MoveFileResult MoveFile(SVR_VideoLocal_Place videoLocalPlace, bool deleteEmpty = true, string scriptName = null)
    {
        try
        {
            if (videoLocalPlace.FullServerPath == null)
            {
                _logger.LogError("Could not find or access the file to move: {VideoLocalPlaceID}", videoLocalPlace.VideoLocal_Place_ID);
                return new MoveFileResult { CanRetry = true, NewFolder = string.Empty, ErrorMessage = "Unable to access file" };
            }

            if (!File.Exists(videoLocalPlace.FullServerPath))
            {
                _logger.LogError("Could not find or access the file to move: \"{FullServerPath}\"", videoLocalPlace.FullServerPath);
                // Retry logic can be added here if needed
                return new MoveFileResult { CanRetry = true, NewFolder = string.Empty, ErrorMessage = "Could not access the file" };
            }

            var sourceFile = new FileInfo(videoLocalPlace.FullServerPath);

            var (destImpl, newFolderPath) = RenameFileHelper.GetDestination(videoLocalPlace, scriptName);

            if (destImpl is not SVR_ImportFolder destFolder)
            {
                if (newFolderPath != null)
                {
                    _logger.LogError("Unable to find destination for: \"{FullServerPath}\"", videoLocalPlace.FullServerPath);
                    _logger.LogError("The error message was: {NewFolderPath}", newFolderPath);
                    return new MoveFileResult { NewFolder = string.Empty, ErrorMessage = newFolderPath };
                }

                _logger.LogError("Unable to find destination for: \"{FullServerPath}\"", videoLocalPlace.FullServerPath);
                return new MoveFileResult { NewFolder = string.Empty, ErrorMessage = "There was an error but no error code returned..." };
            }

            var dropFolder = videoLocalPlace.ImportFolder;

            if (string.IsNullOrEmpty(newFolderPath))
            {
                _logger.LogError("Unable to find destination for: \"{FullServerPath}\"", videoLocalPlace.FullServerPath);
                return new MoveFileResult { NewFolder = string.Empty, ErrorMessage = "The returned path was null or empty" };
            }

            var newFilePath = Path.Combine(newFolderPath, Path.GetFileName(videoLocalPlace.FullServerPath));
            var newFullServerPath = Path.Combine(destFolder.ImportFolderLocation, newFilePath);

            var destFullTree = Path.Combine(destFolder.ImportFolderLocation, newFolderPath);
            if (!Directory.Exists(destFullTree))
            {
                try
                {
                    Directory.CreateDirectory(destFullTree);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to create directory tree: {Ex}", e);
                    return new MoveFileResult { CanRetry = true, NewFolder = string.Empty, ErrorMessage = $"Unable to create directory tree: \"{destFullTree}\"", Exception = e };
                }
            }

            if (newFullServerPath.Equals(videoLocalPlace.FullServerPath, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogInformation("Moving file SKIPPED! The file is already at its desired location: \"{FullServerPath}\"", videoLocalPlace.FullServerPath);
                return new MoveFileResult { IsSuccess = true, NewFolder = newFolderPath };
            }

            if (File.Exists(newFullServerPath))
            {
                _logger.LogError("A file already exists at the desired location: \"{FullServerPath}\"", videoLocalPlace.FullServerPath);
                return new MoveFileResult { NewFolder = string.Empty, ErrorMessage = "A file already exists at the destination" };
            }

            Utils.ShokoServer.AddFileWatcherExclusion(newFullServerPath);

            _logger.LogInformation("Moving file from \"{FullServerPath}\" to \"{NewFullServerPath}\"", videoLocalPlace.FullServerPath, newFullServerPath);
            try
            {
                sourceFile.MoveTo(newFullServerPath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to MOVE file: \"{FullServerPath}\" to \"{NewFullServerPath}\" Error: {Ex}", videoLocalPlace.FullServerPath,
                    newFullServerPath, e);
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullServerPath);
                return new MoveFileResult { CanRetry = true, NewFolder = newFolderPath, ErrorMessage = e.Message, Exception = e };
            }

            var originalFileName = videoLocalPlace.FullServerPath;
            var oldPath = videoLocalPlace.FilePath;

            videoLocalPlace.ImportFolderID = destFolder.ImportFolderID;
            videoLocalPlace.FilePath = newFilePath;
            RepoFactory.VideoLocalPlace.Save(videoLocalPlace);

            MoveExternalSubtitles(newFullServerPath, originalFileName);

            if (dropFolder.IsDropSource == 1 && deleteEmpty)
            {
                RecursiveDeleteEmptyDirectories(dropFolder.ImportFolderLocation, true);
            }

            ShokoEventHandler.Instance.OnFileMoved(dropFolder, destFolder, oldPath, newFilePath, videoLocalPlace);
            Utils.ShokoServer.RemoveFileWatcherExclusion(newFullServerPath);

            return new MoveFileResult { IsSuccess = true, NewFolder = newFolderPath };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not MOVE file: \"{FullServerPath}\" -- {Ex}",
                videoLocalPlace.FullServerPath ?? videoLocalPlace.VideoLocal_Place_ID.ToString(), ex);
            return new MoveFileResult { CanRetry = true, NewFolder = string.Empty, ErrorMessage = ex.Message, Exception = ex};
        }
    }

    private void MoveExternalSubtitles(string newFullServerPath, string originalFileName)
    {
        try
        {
            var textStreams = SubtitleHelper.GetSubtitleStreams(originalFileName);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;

                var newParent = Path.GetDirectoryName(newFullServerPath);
                var srcParent = Path.GetDirectoryName(originalFileName);
                if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(srcParent)) continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath)) continue;

                var subFile = new FileInfo(subPath);
                var newSubPath = Path.Combine(newParent, subFile.Name);
                if (File.Exists(newSubPath))
                {
                    try
                    {
                        File.Delete(newSubPath);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Unable to DELETE file: \"{SubtitleFile}\" error {Ex}", subtitleFile.Filename, e);
                    }
                }

                try
                {
                    subFile.MoveTo(newSubPath);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to MOVE file: \"{SubtitleFile}\" to \"{NewSubPath}\" error {Ex}", subtitleFile.Filename, newSubPath, e);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to move subtitles for \"{Place}\": {Ex}", originalFileName, ex.ToString());
        }
    }

    private void RecursiveDeleteEmptyDirectories(string dir, bool importFolder)
    {
        try
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (!Directory.Exists(dir)) return;
            if (_settingsProvider.GetSettings().Import.Exclude.Any(s => Regex.IsMatch(dir, s))) return;

            if (IsDirectoryEmpty(dir))
            {
                if (importFolder) return;

                try
                {
                    Directory.Delete(dir);
                }
                catch (Exception ex)
                {
                    if (ex is DirectoryNotFoundException or FileNotFoundException) return;
                    _logger.LogWarning(ex, "Unable to DELETE directory: {Directory} Error: {Ex}", dir, ex);
                }

                return;
            }

            // If it has folder, recurse
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                if (_settingsProvider.GetSettings().Import.Exclude.Any(s => Regex.IsMatch(d, s))) continue;
                RecursiveDeleteEmptyDirectories(d, false);
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException) return;
            _logger.LogError(e, "There was an error removing the empty directory: {Dir}\n{Ex}", dir, e);
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
    
    public bool RefreshMediaInfo(SVR_VideoLocal_Place place)
    {
        try
        {
            _logger.LogTrace("Getting media info for: {Place}", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
            MediaContainer m = null;
            if (place.VideoLocal == null)
            {
                _logger.LogError("VideoLocal for {Place} failed to be retrieved for MediaInfo", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
                return false;
            }

            if (place.FullServerPath != null)
            {
                if (place.GetFile() == null)
                {
                    _logger.LogError("File {Place} failed to be retrieved for MediaInfo", place.FullServerPath ?? place.VideoLocal_Place_ID.ToString());
                    return false;
                }

                var name = place.FullServerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
                m = Utilities.MediaInfoLib.MediaInfo.GetMediaInfo(name); //Mediainfo should have libcurl.dll for http
                var duration = m?.GeneralStream?.Duration ?? 0;
                if (duration == 0)
                {
                    m = null;
                }
            }


            if (m != null)
            {
                var info = place.VideoLocal;

                var subs = SubtitleHelper.GetSubtitleStreams(place.FullServerPath);
                if (subs.Count > 0)
                {
                    m.media.track.AddRange(subs);
                }

                info.Media = m;
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
        {
            RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.FullServerPath), true);
        }

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

            if (deleteFolders) RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.FullServerPath), true);
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

        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            if (v?.Places?.Count <= 1)
            {
                if (updateMyListStatus)
                {
                    if (RepoFactory.AniDB_File.GetByHash(v.Hash) == null)
                    {
                        var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(v.Hash);
                        foreach (var xref in xrefs)
                        {
                            var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                            if (ep == null) continue;

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
                    ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder, place);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);

                    seriesToUpdate.AddRange(v.GetAnimeEpisodes().DistinctBy(a => a.AnimeSeriesID)
                        .Select(a => a.GetAnimeSeries()));
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, v);
                    transaction.Commit();
                });
            }
            else
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder, place);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
                    transaction.Commit();
                });
            }
        }

        foreach (var ser in seriesToUpdate)
        {
            ser?.QueueUpdateStats();
        }
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
                if (RepoFactory.AniDB_File.GetByHash(v.Hash) == null)
                {
                    var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(v.Hash);
                    foreach (var xref in xrefs)
                    {
                        var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                        if (ep == null)
                        {
                            continue;
                        }

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

            var eps = v.GetAnimeEpisodes()?.Where(a => a != null).ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.GetAnimeSeries()).ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder, place);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, place);
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
                transaction.Commit();
            });
        }
        else
        {
            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(place.ImportFolder, place);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, place);
                transaction.Commit();
            });
        }
    }
}
