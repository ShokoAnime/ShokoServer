using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Directory = System.IO.Directory;
using MediaContainer = Shoko.Models.MediaInfo.MediaContainer;

namespace Shoko.Server.Models;

public enum DELAY_IN_USE
{
    FIRST = 750,
    SECOND = 3000,
    THIRD = 5000
}

public class SVR_VideoLocal_Place : VideoLocal_Place, IVideoFile
{
    internal SVR_ImportFolder ImportFolder => RepoFactory.ImportFolder.GetByID(ImportFolderID);

    public string FullServerPath
    {
        get
        {
            if (string.IsNullOrEmpty(ImportFolder?.ImportFolderLocation) || string.IsNullOrEmpty(FilePath))
            {
                return null;
            }

            return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public SVR_VideoLocal VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    private static Logger logger = LogManager.GetCurrentClassLogger();

    // returns false if we should try again after the timer
    // TODO Generify this and Move and make a return model instead of tuple
    public (bool, string, string) RenameFile(bool preview = false, string scriptName = null)
    {
        if (scriptName != null && scriptName.Equals(Shoko.Models.Constants.Renamer.TempFileName))
        {
            return (true, string.Empty, "Error: Do not attempt to use a temp file to rename.");
        }

        if (ImportFolder == null)
        {
            logger.Error(
                $"Error: The renamer can't get the import folder for ImportFolderID: {ImportFolderID}, File: \"{FilePath}\"");
            return (true, string.Empty, "Error: Could not find the file");
        }

        var renamed = RenameFileHelper.GetFilename(this, scriptName);
        if (string.IsNullOrEmpty(renamed))
        {
            logger.Error($"Error: The renamer returned a null or empty name for: \"{FullServerPath}\"");
            return (true, string.Empty, "Error: The file renamer returned a null or empty value");
        }

        if (renamed.StartsWith("*Error: "))
        {
            logger.Error($"Error: The renamer returned an error on file: \"{FullServerPath}\"\n            {renamed}");
            return (true, string.Empty, renamed.Substring(1));
        }

        // actually rename the file
        var fullFileName = FullServerPath;

        // check if the file exists
        if (string.IsNullOrEmpty(fullFileName))
        {
            logger.Error($"Error could not find the original file for renaming, or it is in use: \"{fullFileName}\"");
            return (false, renamed, "Error: Could not access the file");
        }

        if (!File.Exists(fullFileName))
        {
            logger.Error($"Error could not find the original file for renaming, or it is in use: \"{fullFileName}\"");
            return (false, renamed, "Error: Could not access the file");
        }

        // actually rename the file
        var path = Path.GetDirectoryName(fullFileName);
        var newFullName = Path.Combine(path, renamed);

        try
        {
            if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
            {
                logger.Info($"Renaming file SKIPPED! no change From \"{fullFileName}\" to \"{newFullName}\"");
                return (true, renamed, string.Empty);
            }

            if (File.Exists(newFullName))
            {
                logger.Info($"Renaming file SKIPPED! Destination Exists \"{newFullName}\"");
                return (true, renamed, "Error: The filename already exists");
            }

            if (preview)
            {
                return (false, renamed, string.Empty);
            }

            Utils.ShokoServer.AddFileWatcherExclusion(newFullName);

            logger.Info($"Renaming file From \"{fullFileName}\" to \"{newFullName}\"");
            try
            {
                var file = new FileInfo(fullFileName);
                file.MoveTo(newFullName);
            }
            catch (Exception e)
            {
                logger.Info($"Renaming file FAILED! From \"{fullFileName}\" to \"{newFullName}\" - {e}");
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullName);
                return (false, renamed, "Error: Failed to rename file");
            }

            // Rename external subs!
            RenameExternalSubtitles(fullFileName, renamed);

            logger.Info($"Renaming file SUCCESS! From \"{fullFileName}\" to \"{newFullName}\"");
            var tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
            if (tup == null)
            {
                logger.Error($"Unable to LOCATE file \"{newFullName}\" inside the import folders");
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullName);
                return (false, renamed, "Error: Unable to resolve new path");
            }

            // Before we change all references, remap Duplicate Files
            var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);
            if (dups != null && dups.Count > 0)
            {
                foreach (var dup in dups)
                {
                    var dupchanged = false;
                    if (dup.FilePathFile1.Equals(FilePath, StringComparison.InvariantCultureIgnoreCase) &&
                        dup.ImportFolderIDFile1 == ImportFolderID)
                    {
                        dup.FilePathFile1 = tup.Item2;
                        dupchanged = true;
                    }
                    else if (dup.FilePathFile2.Equals(FilePath, StringComparison.InvariantCultureIgnoreCase) &&
                             dup.ImportFolderIDFile2 == ImportFolderID)
                    {
                        dup.FilePathFile2 = tup.Item2;
                        dupchanged = true;
                    }

                    if (dupchanged)
                    {
                        RepoFactory.DuplicateFile.Save(dup);
                    }
                }
            }

            // Rename hash xrefs
            var filenameHash = RepoFactory.FileNameHash.GetByHash(VideoLocal.Hash);
            if (!filenameHash.Any(a => a.FileName.Equals(renamed)))
            {
                var fnhash = new FileNameHash
                {
                    DateTimeUpdated = DateTime.Now,
                    FileName = renamed,
                    FileSize = VideoLocal.FileSize,
                    Hash = VideoLocal.Hash
                };
                RepoFactory.FileNameHash.Save(fnhash);
            }

            FilePath = tup.Item2;
            RepoFactory.VideoLocalPlace.Save(this);
            // just in case
            VideoLocal.FileName = renamed;
            RepoFactory.VideoLocal.Save(VideoLocal, false);
            
            ShokoEventHandler.Instance.OnFileRenamed(ImportFolder, Path.GetFileName(fullFileName), renamed, this);
        }
        catch (Exception ex)
        {
            logger.Info($"Renaming file FAILED! From \"{fullFileName}\" to \"{newFullName}\" - {ex.Message}");
            logger.Error(ex, ex.ToString());
            return (true, string.Empty, $"Error: {ex.Message}");
        }

        Utils.ShokoServer.RemoveFileWatcherExclusion(newFullName);
        return (true, renamed, string.Empty);
    }

    public void RemoveRecord(bool updateMyListStatus = true)
    {
        logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
        var seriesToUpdate = new List<SVR_AnimeSeries>();
        var v = VideoLocal;
        List<DuplicateFile> dupFiles = null;
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        if (!string.IsNullOrEmpty(FilePath))
        {
            dupFiles = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);
        }

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

                            var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(
                                c =>
                                {
                                    c.AnimeID = xref.AnimeID;
                                    c.EpisodeType = ep.GetEpisodeTypeEnum();
                                    c.EpisodeNumber = ep.EpisodeNumber;
                                }
                            );
                            cmdDel.Save();
                        }
                    }
                    else
                    {
                        var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(
                            c =>
                            {
                                c.Hash = v.Hash;
                                c.FileSize = v.FileSize;
                            }
                        );
                        cmdDel.Save();
                    }
                }

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(() =>
                {
                    using var transaction = session.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);

                    seriesToUpdate.AddRange(v.GetAnimeEpisodes().DistinctBy(a => a.AnimeSeriesID)
                        .Select(a => a.GetAnimeSeries()));
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);

                    dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));
                    transaction.Commit();
                });
            }
            else
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(() =>
                {
                    using var transaction = session.BeginTransaction();
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                    dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));
                    transaction.Commit();
                });
            }
        }

        foreach (var ser in seriesToUpdate)
        {
            ser?.QueueUpdateStats();
        }
    }


    public void RemoveRecordWithOpenTransaction(ISession session, ICollection<SVR_AnimeSeries> seriesToUpdate,
        bool updateMyListStatus = true, bool removeDuplicateFileEntries = true)
    {
        logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
        var v = VideoLocal;
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();

        List<DuplicateFile> dupFiles = null;
        if (!string.IsNullOrEmpty(FilePath) && removeDuplicateFileEntries)
        {
            dupFiles = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);
        }

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
                        if (ep == null)
                        {
                            continue;
                        }

                        var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(c =>
                        {
                            c.AnimeID = xref.AnimeID;
                            c.EpisodeType = ep.GetEpisodeTypeEnum();
                            c.EpisodeNumber = ep.EpisodeNumber;
                        });
                        cmdDel.Save();
                    }
                }
                else
                {
                    var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(
                        c =>
                        {
                            c.Hash = v.Hash;
                            c.FileSize = v.FileSize;
                        }
                    );
                    cmdDel.Save();
                }
            }

            var eps = v?.GetAnimeEpisodes()?.Where(a => a != null).ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.GetAnimeSeries()).ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
                dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));

                transaction.Commit();
            });
        }
        else
        {
            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(ImportFolder, this);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));
                transaction.Commit();
            });
        }
    }

    public FileInfo GetFile()
    {
        if (!File.Exists(FullServerPath))
        {
            return null;
        }

        return new FileInfo(FullServerPath);
    }

    public bool RefreshMediaInfo()
    {
        try
        {
            logger.Trace("Getting media info for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
            MediaContainer m = null;
            if (VideoLocal == null)
            {
                logger.Error(
                    $"VideoLocal for {FullServerPath ?? VideoLocal_Place_ID.ToString()} failed to be retrived for MediaInfo");
                return false;
            }

            if (FullServerPath != null)
            {
                if (GetFile() == null)
                {
                    logger.Error(
                        $"File {FullServerPath ?? VideoLocal_Place_ID.ToString()} failed to be retrived for MediaInfo");
                    return false;
                }

                var name = FullServerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
                m = Utilities.MediaInfoLib.MediaInfo.GetMediaInfo(name); //Mediainfo should have libcurl.dll for http
                var duration = m?.GeneralStream?.Duration ?? 0;
                if (duration == 0)
                {
                    m = null;
                }
            }


            if (m != null)
            {
                var info = VideoLocal;

                var subs = SubtitleHelper.GetSubtitleStreams(FullServerPath);
                if (subs.Count > 0)
                {
                    m.media.track.AddRange(subs);
                }

                info.Media = m;
                return true;
            }

            logger.Error($"File {FullServerPath ?? VideoLocal_Place_ID.ToString()} failed to read MediaInfo");
        }
        catch (Exception e)
        {
            logger.Error(
                $"Unable to read the media information of file {FullServerPath ?? VideoLocal_Place_ID.ToString()} ERROR: {e}");
        }

        return false;
    }

    [Obsolete]
    public (bool, string) RemoveAndDeleteFile(bool deleteFolder = true)
    {
        // TODO Make this take an argument to disable removing empty dirs. It's slow, and should only be done if needed
        try
        {
            logger.Info("Deleting video local place record and file: {0}",
                FullServerPath ?? VideoLocal_Place_ID.ToString());

            if (!File.Exists(FullServerPath))
            {
                logger.Info($"Unable to find file. Removing Record: {FullServerPath ?? FilePath}");
                RemoveRecord();
                return (true, string.Empty);
            }

            try
            {
                File.Delete(FullServerPath);
                DeleteExternalSubtitles(FullServerPath);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                {
                    if (deleteFolder)
                    {
                        RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
                    }

                    RemoveRecord();
                    return (true, string.Empty);
                }

                logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                return (false, $"Unable to delete file '{FullServerPath}'");
            }

            if (deleteFolder)
            {
                RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
            }

            RemoveRecord();
            // For deletion of files from Trakt, we will rely on the Daily sync
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return (false, ex.Message);
        }
    }

    public void RemoveRecordAndDeletePhysicalFile(bool deleteFolder = true)
    {
        logger.Info("Deleting video local place record and file: {0}",
            FullServerPath ?? VideoLocal_Place_ID.ToString());

        if (!File.Exists(FullServerPath))
        {
            logger.Info($"Unable to find file. Removing Record: {FullServerPath ?? FilePath}");
            RemoveRecord();
            return;
        }

        try
        {
            File.Delete(FullServerPath);
            DeleteExternalSubtitles(FullServerPath);
        }
        catch (FileNotFoundException)
        {
            if (deleteFolder)
            {
                RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
            }

            RemoveRecord();
            return;
        }
        catch (Exception ex)
        {
            logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
            throw;
        }

        if (deleteFolder)
        {
            RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
        }

        RemoveRecord();
    }

    public void RemoveAndDeleteFileWithOpenTransaction(ISession session, HashSet<SVR_AnimeSeries> seriesToUpdate)
    {
        // TODO Make this take an argument to disable removing empty dirs. It's slow, and should only be done if needed
        try
        {
            logger.Info("Deleting video local place record and file: {0}",
                FullServerPath ?? VideoLocal_Place_ID.ToString());


            if (!File.Exists(FullServerPath))
            {
                logger.Info($"Unable to find file. Removing Record: {FullServerPath}");
                RemoveRecordWithOpenTransaction(session, seriesToUpdate);
                return;
            }

            try
            {
                File.Delete(FullServerPath);
                DeleteExternalSubtitles(FullServerPath);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                {
                    RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
                    RemoveRecordWithOpenTransaction(session, seriesToUpdate);
                    return;
                }

                logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                return;
            }

            RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
            RemoveRecordWithOpenTransaction(session, seriesToUpdate);
            // For deletion of files from Trakt, we will rely on the Daily sync
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    public void RenameAndMoveAsRequired()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var invert = settings.Import.RenameThenMove;
        // Move first so that we don't bother the filesystem watcher
        var succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
        if (!succeeded)
        {
            Thread.Sleep((int)DELAY_IN_USE.FIRST);
            succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
            if (!succeeded)
            {
                Thread.Sleep((int)DELAY_IN_USE.SECOND);
                succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
                if (!succeeded)
                {
                    Thread.Sleep((int)DELAY_IN_USE.THIRD);
                    succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
                    if (!succeeded)
                    {
                        return; // Don't bother renaming if we couldn't move. It'll need user interaction
                    }
                }
            }
        }

        succeeded = invert ? MoveFileIfRequired() : RenameIfRequired();
        if (!succeeded)
        {
            Thread.Sleep((int)DELAY_IN_USE.FIRST);
            succeeded = invert ? MoveFileIfRequired() : RenameIfRequired();
            if (!succeeded)
            {
                Thread.Sleep((int)DELAY_IN_USE.SECOND);
                succeeded = invert ? MoveFileIfRequired() : RenameIfRequired();
                if (!succeeded)
                {
                    Thread.Sleep((int)DELAY_IN_USE.THIRD);
                    succeeded = invert ? MoveFileIfRequired() : RenameIfRequired();
                    if (!succeeded)
                    {
                        return;
                    }
                }
            }
        }

        Utils.ShokoServer.AddFileWatcherExclusion(FullServerPath);
        try
        {
            LinuxFS.SetLinuxPermissions(FullServerPath, settings.Linux.UID,
                settings.Linux.GID, settings.Linux.Permission);
        }
        catch (InvalidOperationException e)
        {
            logger.Error(e, $"Unable to set permissions ({settings.Linux.UID}:{settings.Linux.GID} {settings.Linux.Permission}) on file {FileName}: Access Denied");
        }
        catch (Exception e)
        {
            logger.Error(e, "Error setting Linux Permissions: {0}", e);
        }
        Utils.ShokoServer.RemoveFileWatcherExclusion(FullServerPath);
    }

    // returns false if we should retry
    private bool RenameIfRequired()
    {
        if (!Utils.SettingsProvider.GetSettings().Import.RenameOnImport)
        {
            logger.Trace($"Skipping rename of \"{FullServerPath}\" as rename on import is disabled");
            return true;
        }

        try
        {
            return RenameFile().Item1;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return true;
        }
    }

    // TODO Merge these, with proper logic depending on the scenario (import, force, etc)
    public (string, string) MoveWithResultString(string scriptName, bool force = false)
    {
        // TODO Make this take an argument to disable removing empty dirs. It's slow, and should only be done if needed
        if (FullServerPath == null)
        {
            logger.Error($"Could not find or access the file to move: {VideoLocal_Place_ID}");
            return (string.Empty, "ERROR: Unable to access file");
        }

        if (!File.Exists(FullServerPath))
        {
            logger.Error($"Could not find or access the file to move: \"{FullServerPath}\"");
            // this can happen due to file locks, so retry
            return (string.Empty, "ERROR: Could not access the file");
        }

        var sourceFile = new FileInfo(FullServerPath);

        // There is a possibility of weird logic based on source of the file. Some handling should be made for it....later
        var (destImpl, newFolderPath) = RenameFileHelper.GetDestination(this, scriptName);

        if (!(destImpl is SVR_ImportFolder destFolder))
        {
            // In this case, an error string was returned, but we'll suppress it and give an error elsewhere
            if (newFolderPath != null)
            {
                logger.Error($"Unable to find destination for: \"{FullServerPath}\"");
                logger.Error($"The error message was: {newFolderPath}");
                return (string.Empty, "ERROR: " + newFolderPath);
            }

            logger.Error($"Unable to find destination for: \"{FullServerPath}\"");
            return (string.Empty, "ERROR: There was an error but no error code returned...");
        }

        // keep the original drop folder for later (take a copy, not a reference)
        var dropFolder = ImportFolder;

        if (string.IsNullOrEmpty(newFolderPath))
        {
            logger.Error($"Unable to find destination for: \"{FullServerPath}\"");
            return (string.Empty, "ERROR: The returned path was null or empty");
        }

        // We've already resolved FullServerPath, so it doesn't need to be checked
        var newFilePath = Path.Combine(newFolderPath, Path.GetFileName(FullServerPath));
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
                logger.Error(e);
                return (string.Empty, $"ERROR: Unable to create directory tree: \"{destFullTree}\"");
            }
        }

        // Last ditch effort to ensure we aren't moving a file unto itself
        if (newFullServerPath.Equals(FullServerPath, StringComparison.InvariantCultureIgnoreCase))
        {
            logger.Info($"Moving file SKIPPED! The file is already at its desired location: \"{FullServerPath}\"");
            return (newFolderPath, string.Empty);
        }

        if (File.Exists(newFullServerPath))
        {
            logger.Error($"A file already exists at the desired location: \"{FullServerPath}\"");
            return (string.Empty, "ERROR: A file already exists at the destination");
        }

        Utils.ShokoServer.AddFileWatcherExclusion(newFullServerPath);

        logger.Info($"Moving file from \"{FullServerPath}\" to \"{newFullServerPath}\"");
        try
        {
            sourceFile.MoveTo(newFullServerPath);
        }
        catch (Exception e)
        {
            logger.Error($"Unable to MOVE file: \"{FullServerPath}\" to \"{newFullServerPath}\" error {e}");
            Utils.ShokoServer.RemoveFileWatcherExclusion(newFullServerPath);
            return (newFullServerPath, "ERROR: " + e);
        }

        // Save for later. Scan for subtitles while the vlplace is still set for the source location
        var originalFileName = FullServerPath;
        var oldPath = FilePath;

        MoveDuplicateFiles(newFilePath, destFolder);

        ImportFolderID = destFolder.ImportFolderID;
        FilePath = newFilePath;
        RepoFactory.VideoLocalPlace.Save(this);

        MoveExternalSubtitles(newFullServerPath, originalFileName);

        // check for any empty folders in drop folder
        // only for the drop folder
        if (dropFolder.IsDropSource == 1)
        {
            RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
        }

        ShokoEventHandler.Instance.OnFileMoved(dropFolder, destFolder, oldPath, newFilePath, this);
        Utils.ShokoServer.RemoveFileWatcherExclusion(newFullServerPath);
        return (newFolderPath, string.Empty);
    }

    // returns false if we should retry
    private bool MoveFileIfRequired(bool deleteEmpty = true)
    {
        // TODO move A LOT of this into renamer helper methods. A renamer can do them optionally
        if (!Utils.SettingsProvider.GetSettings().Import.MoveOnImport)
        {
            logger.Trace($"Skipping move of \"{FullServerPath}\" as move on import is disabled");
            return true;
        }

        // TODO Make this take an argument to disable removing empty dirs. It's slow, and should only be done if needed
        try
        {
            logger.Trace($"Attempting to MOVE file: \"{FullServerPath ?? VideoLocal_Place_ID.ToString()}\"");

            if (FullServerPath == null)
            {
                logger.Error($"Could not find or access the file to move: {VideoLocal_Place_ID}");
                return true;
            }

            // check if this file is in the drop folder
            // otherwise we don't need to move it
            if (ImportFolder.IsDropSource == 0)
            {
                logger.Trace($"Not moving file as it is NOT in the drop folder: \"{FullServerPath}\"");
                return true;
            }

            if (!File.Exists(FullServerPath))
            {
                logger.Error($"Could not find or access the file to move: \"{FullServerPath}\"");
                // this can happen due to file locks, so retry
                return false;
            }

            var sourceFile = new FileInfo(FullServerPath);

            // find the default destination
            var (destImpl, newFolderPath) = RenameFileHelper.GetDestination(this, null);

            if (!(destImpl is SVR_ImportFolder destFolder))
            {
                // In this case, an error string was returned, but we'll suppress it and give an error elsewhere
                if (newFolderPath != null)
                {
                    return true;
                }

                logger.Error($"Could not find a valid destination: \"{FullServerPath}\"");
                return true;
            }

            // keep the original drop folder for later (take a copy, not a reference)
            var dropFolder = ImportFolder;

            if (string.IsNullOrEmpty(newFolderPath))
            {
                return true;
            }

            // We've already resolved FullServerPath, so it doesn't need to be checked
            var newFilePath = Path.Combine(newFolderPath, Path.GetFileName(FullServerPath));
            var newFullServerPath = Path.Combine(destFolder.ImportFolderLocation, newFilePath);

            var destFullTree = Path.Combine(destFolder.ImportFolderLocation, newFolderPath);
            if (!Directory.Exists(destFullTree))
            {
                try
                {
                    Utils.ShokoServer.AddFileWatcherExclusion(destFullTree);
                    Directory.CreateDirectory(destFullTree);
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    return true;
                }
                finally
                {
                    Utils.ShokoServer.RemoveFileWatcherExclusion(destFullTree);
                }
            }

            // Last ditch effort to ensure we aren't moving a file unto itself
            if (newFullServerPath.Equals(FullServerPath, StringComparison.InvariantCultureIgnoreCase))
            {
                logger.Error($"Resolved to move \"{newFullServerPath}\" unto itself. NOT MOVING");
                return true;
            }

            var originalFileName = FullServerPath;
            var oldPath = FilePath;

            if (File.Exists(newFullServerPath))
            {
                // A file with the same name exists at the destination.
                // Handle Duplicate Files, A duplicate file record won't exist yet,
                // so we'll check the old fashioned way
                logger.Trace("A file already exists at the new location, checking it for duplicate");
                var destVideoLocalPlace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(newFilePath,
                    destFolder.ImportFolderID);
                var destVideoLocal = destVideoLocalPlace?.VideoLocal;
                if (destVideoLocal == null)
                {
                    logger.Error("The existing file at the new location does not have a VideoLocal. Not moving");
                    return true;
                }

                if (destVideoLocal.Hash == VideoLocal.Hash)
                {
                    logger.Info(
                        $"Not moving file as it already exists at the new location, deleting source file instead: \"{FullServerPath}\" --- \"{newFullServerPath}\"");

                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    try
                    {
                        sourceFile.Delete();
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Unable to DELETE file: \"{FullServerPath}\" error {e}");
                        RemoveRecord(false);

                        // check for any empty folders in drop folder
                        // only for the drop folder
                        if (dropFolder.IsDropSource != 1)
                        {
                            return true;
                        }

                        RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
                        return true;
                    }
                }

                // Not a dupe, don't delete it
                logger.Trace("A file already exists at the new location, checking it for version and group");
                var destinationExistingAniDBFile = destVideoLocal.GetAniDBFile();
                if (destinationExistingAniDBFile == null)
                {
                    logger.Error("The existing file at the new location does not have AniDB info. Not moving.");
                    return true;
                }

                var aniDBFile = VideoLocal.GetAniDBFile();
                if (aniDBFile == null)
                {
                    logger.Error("The file does not have AniDB info. Not moving.");
                    return true;
                }

                if (destinationExistingAniDBFile.Anime_GroupName == aniDBFile.Anime_GroupName &&
                    destinationExistingAniDBFile.FileVersion < aniDBFile.FileVersion)
                {
                    // This is a V2 replacing a V1 with the same name.
                    // Normally we'd let the Multiple Files Utility handle it, but let's just delete the V1
                    logger.Info("The existing file is a V1 from the same group. Replacing it.");
                    // Delete the destination
                    var (success, _) = destVideoLocalPlace.RemoveAndDeleteFile();
                    if (!success)
                    {
                        return false;
                    }

                    // Move
                    Utils.ShokoServer.AddFileWatcherExclusion(newFullServerPath);
                    logger.Info($"Moving file from \"{FullServerPath}\" to \"{newFullServerPath}\"");
                    try
                    {
                        sourceFile.MoveTo(newFullServerPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to MOVE file: \"{FullServerPath}\" to \"{newFullServerPath}\" error {e}");
                        Utils.ShokoServer.RemoveFileWatcherExclusion(newFullServerPath);
                        return false;
                    }

                    MoveDuplicateFiles(newFilePath, destFolder);

                    ImportFolderID = destFolder.ImportFolderID;
                    FilePath = newFilePath;
                    RepoFactory.VideoLocalPlace.Save(this);

                    // check for any empty folders in drop folder
                    // only for the drop folder
                    if (dropFolder.IsDropSource == 1 && deleteEmpty)
                    {
                        RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
                    }
                }
            }
            else
            {
                Utils.ShokoServer.AddFileWatcherExclusion(newFullServerPath);
                logger.Info($"Moving file from \"{FullServerPath}\" to \"{newFullServerPath}\"");
                try
                {
                    sourceFile.MoveTo(newFullServerPath);
                }
                catch (Exception e)
                {
                    logger.Error($"Unable to MOVE file: \"{FullServerPath}\" to \"{newFullServerPath}\" error {e}");
                    Utils.ShokoServer.RemoveFileWatcherExclusion(newFullServerPath);
                    return false;
                }

                MoveDuplicateFiles(newFilePath, destFolder);

                ImportFolderID = destFolder.ImportFolderID;
                FilePath = newFilePath;
                RepoFactory.VideoLocalPlace.Save(this);

                // check for any empty folders in drop folder
                // only for the drop folder
                if (dropFolder.IsDropSource == 1 && deleteEmpty)
                {
                    RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
                }
            }

            MoveExternalSubtitles(newFullServerPath, originalFileName);
            ShokoEventHandler.Instance.OnFileMoved(dropFolder, destFolder, oldPath, newFilePath, this);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Could not MOVE file: \"{FullServerPath ?? VideoLocal_Place_ID.ToString()}\" -- {ex}");
        }

        return true;
    }

    private static void MoveExternalSubtitles(string newFullServerPath, string originalFileName)
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
                        logger.Warn($"Unable to DELETE file: \"{subtitleFile}\" error {e}");
                    }
                }

                try
                {
                    subFile.MoveTo(newSubPath);
                }
                catch (Exception e)
                {
                    logger.Error($"Unable to MOVE file: \"{subtitleFile}\" to \"{newSubPath}\" error {e}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    private static void RenameExternalSubtitles(string fullFileName, string renamed)
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

            var oldSubPath = Path.Combine(path, sub.Filename);

            if (!File.Exists(oldSubPath))
            {
                logger.Error($"Unable to rename external subtitle \"{sub.Filename}\". Cannot access the file");
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
                logger.Error($"Unable to rename external subtitle \"{sub.Filename}\" to \"{newSub}\". {e}");
            }
        }
    }

    private static void DeleteExternalSubtitles(string originalFileName)
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
                    logger.Error(e, $"Unable to delete file: \"{subtitleFile}\"");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    private void MoveDuplicateFiles(string newFilePath, SVR_ImportFolder destFolder)
    {
        var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID)
            .ToList();

        foreach (var dup in dups)
        {
            // Move source
            if (dup.FilePathFile1.Equals(FilePath) && dup.ImportFolderIDFile1 == ImportFolderID)
            {
                dup.FilePathFile1 = newFilePath;
                dup.ImportFolderIDFile1 = destFolder.ImportFolderID;
            }
            else if (dup.FilePathFile2.Equals(FilePath) && dup.ImportFolderIDFile2 == ImportFolderID)
            {
                dup.FilePathFile2 = newFilePath;
                dup.ImportFolderIDFile2 = destFolder.ImportFolderID;
            }

            // validate the dup file
            // There are cases where a dup file was not cleaned up before, so we'll do it here, too
            if (!dup.GetFullServerPath1()
                    .Equals(dup.GetFullServerPath2(), StringComparison.InvariantCultureIgnoreCase))
            {
                RepoFactory.DuplicateFile.Save(dup);
            }
            else
            {
                RepoFactory.DuplicateFile.Delete(dup);
            }
        }
    }

    private void RecursiveDeleteEmptyDirectories(string dir, bool importfolder)
    {
        try
        {
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            if (!Directory.Exists(dir))
            {
                return;
            }

            if (IsDirectoryEmpty(dir))
            {
                if (importfolder)
                {
                    return;
                }

                try
                {
                    Directory.Delete(dir);
                }
                catch (Exception ex)
                {
                    if (ex is DirectoryNotFoundException || ex is FileNotFoundException)
                    {
                        return;
                    }

                    logger.Warn("Unable to DELETE directory: {0} Error: {1}", dir,
                        ex);
                }

                return;
            }

            // If it has folder, recurse
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                if (Utils.SettingsProvider.GetSettings().Import.Exclude.Any(s => Regex.IsMatch(Path.GetDirectoryName(d) ?? string.Empty, s))) continue;
                RecursiveDeleteEmptyDirectories(d, false);
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                return;
            }

            logger.Error($"There was an error removing the empty directory: {dir}\r\n{e}");
        }
    }

    public bool IsDirectoryEmpty(string path)
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

    int IVideoFile.VideoFileID => VideoLocalID;
    string IVideoFile.Filename => Path.GetFileName(FilePath);
    string IVideoFile.FilePath => FullServerPath;
    long IVideoFile.FileSize => VideoLocal?.FileSize ?? 0;
    public IAniDBFile AniDBFileInfo => VideoLocal?.GetAniDBFile();

    public IHashes Hashes => VideoLocal == null
        ? null
        : new VideoHashes
        {
            CRC = VideoLocal.CRC32, MD5 = VideoLocal.MD5, ED2K = VideoLocal.Hash, SHA1 = VideoLocal.SHA1
        };

    public IMediaContainer MediaInfo => VideoLocal?.Media;

    private class VideoHashes : IHashes
    {
        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string ED2K { get; set; }
        public string SHA1 { get; set; }
    }
}
