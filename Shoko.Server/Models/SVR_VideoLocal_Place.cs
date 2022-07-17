using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.MediaInfoLib;
using Directory = System.IO.Directory;
using MediaContainer = Shoko.Models.MediaInfo.MediaContainer;

namespace Shoko.Server.Models
{
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
                    return null;
                return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
            }
        }

        public SVR_VideoLocal VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

        private static Logger logger = LogManager.GetCurrentClassLogger();

        // returns false if we should try again after the timer
        // TODO Generify this and Move and make a return model instead of tuple
        public (bool, string, string) RenameFile(bool preview = false, string scriptName = null)
        {
            if (scriptName != null && scriptName.Equals(Shoko.Models.Constants.Renamer.TempFileName))
                return (true, string.Empty, "Error: Do not attempt to use a temp file to rename.");
            if (ImportFolder == null)
            {
                logger.Error($"Error: The renamer can't get the import folder for ImportFolderID: {ImportFolderID}, File: \"{FilePath}\"");
                return (true, string.Empty, "Error: Could not find the file");
            }

            string renamed = RenameFileHelper.GetFilename(this, scriptName);
            if (string.IsNullOrEmpty(renamed))
            {
                logger.Error($"Error: The renamer returned a null or empty name for: \"{FilePath}\"");
                return (true, string.Empty, "Error: The file renamer returned a null or empty value");
            }

            if (renamed.StartsWith("*Error: "))
            {
                logger.Error($"Error: The renamer returned an error on file: \"{FilePath}\"\n            {renamed}");
                return (true, string.Empty, renamed.Substring(1));
            }

            // actually rename the file
            string fullFileName = FullServerPath;

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
            string path = Path.GetDirectoryName(fullFileName);
            string newFullName = Path.Combine(path, renamed);
            var textStreams = SubtitleHelper.GetSubtitleStreams(this);

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
                if (preview) return (false, renamed, string.Empty);

                ShokoServer.PauseWatchingFiles();

                logger.Info($"Renaming file From \"{fullFileName}\" to \"{newFullName}\"");
                try
                {
                    var file = new FileInfo(fullFileName);
                    file.MoveTo(newFullName);
                }
                catch (Exception e)
                {
                    logger.Info($"Renaming file FAILED! From \"{fullFileName}\" to \"{newFullName}\" - {e}");
                    ShokoServer.UnpauseWatchingFiles();
                    return (false, renamed, "Error: Failed to rename file");
                }

                // Rename external subs!
                var oldBasename = Path.GetFileNameWithoutExtension(fullFileName);
                var newBasename = Path.GetFileNameWithoutExtension(renamed);
                foreach (TextStream sub in textStreams)
                {
                    if (string.IsNullOrEmpty(sub.Filename)) continue;
                    var oldSubPath = Path.Combine(path, sub.Filename);

                    if (!File.Exists(oldSubPath))
                    {
                        logger.Error($"Unable to rename external subtitle \"{sub.Filename}\". Cannot access the file");
                        continue;
                    }
                    var newSub = sub.Filename.Replace(oldBasename, newBasename);
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

                logger.Info($"Renaming file SUCCESS! From \"{fullFileName}\" to \"{newFullName}\"");
                Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
                if (tup == null)
                {
                    logger.Error($"Unable to LOCATE file \"{newFullName}\" inside the import folders");
                    ShokoServer.UnpauseWatchingFiles();
                    return (false, renamed, "Error: Unable to resolve new path");
                }

                // Before we change all references, remap Duplicate Files
                List<DuplicateFile> dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);
                if (dups != null && dups.Count > 0)
                {
                    foreach (var dup in dups)
                    {
                        bool dupchanged = false;
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
                        if (dupchanged) RepoFactory.DuplicateFile.Save(dup);
                    }
                }
                // Rename hash xrefs
                var filenameHash = RepoFactory.FileNameHash.GetByHash(VideoLocal.Hash);
                if (!filenameHash.Any(a => a.FileName.Equals(renamed)))
                {
                    FileNameHash fnhash = new FileNameHash
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
            }
            catch (Exception ex)
            {
                logger.Info($"Renaming file FAILED! From \"{fullFileName}\" to \"{newFullName}\" - {ex.Message}");
                logger.Error(ex, ex.ToString());
                return (true, string.Empty, $"Error: {ex.Message}");
            }
            ShokoServer.UnpauseWatchingFiles();
            return (true, renamed, string.Empty);
        }

        public void RemoveRecord(bool updateMyListStatus = true)
        {
            logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
            List<SVR_AnimeSeries> seriesToUpdate = new List<SVR_AnimeSeries>();
            SVR_VideoLocal v = VideoLocal;
            List<DuplicateFile> dupFiles = null;
            if (!string.IsNullOrEmpty(FilePath))
                dupFiles = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                if (v?.Places?.Count <= 1)
                {
                    if (updateMyListStatus)
                    {
                         var cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                        cmdDel.Save();
                    }

                    using (var transaction = session.BeginTransaction())
                    {
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);

                        seriesToUpdate.AddRange(v.GetAnimeEpisodes().DistinctBy(a => a.AnimeSeriesID)
                            .Select(a => a.GetAnimeSeries()));
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);

                        dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));
                        transaction.Commit();
                    }
                }
                else
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                        dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));
                        transaction.Commit();
                    }
                }
            }
            foreach (SVR_AnimeSeries ser in seriesToUpdate)
            {
                ser?.QueueUpdateStats();
            }
        }


        public void RemoveRecordWithOpenTransaction(ISession session, ICollection<SVR_AnimeSeries> seriesToUpdate, bool updateMyListStatus = true, bool removeDuplicateFileEntries = true)
        {
            logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
            SVR_VideoLocal v = VideoLocal;

            List<DuplicateFile> dupFiles = null;
            if (!string.IsNullOrEmpty(FilePath) && removeDuplicateFileEntries)
                dupFiles = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);

            if (v?.Places?.Count <= 1)
            {
                if (updateMyListStatus)
                {
                    var cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                    cmdDel.Save();
                }

                List<SVR_AnimeEpisode> eps = v?.GetAnimeEpisodes()?.Where(a => a != null).ToList();
                eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.GetAnimeSeries()).ToList().ForEach(seriesToUpdate.Add);
                using (var transaction = session.BeginTransaction())
                {
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
                    dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));

                    transaction.Commit();
                }
            }
            else
            {
                using (var transaction = session.BeginTransaction())
                {
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                    dupFiles?.ForEach(a => RepoFactory.DuplicateFile.DeleteWithOpenTransaction(session, a));
                    transaction.Commit();
                }
            }
        }

        public FileInfo GetFile()
        {
            if (!File.Exists(FullServerPath)) return null;
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
                    logger.Error($"VideoLocal for {FullServerPath ?? VideoLocal_Place_ID.ToString()} failed to be retrived for MediaInfo");
                    return false;
                }

                if (FullServerPath != null)
                {
                    if (GetFile() == null)
                    {
                        logger.Error($"File {FullServerPath ?? VideoLocal_Place_ID.ToString()} failed to be retrived for MediaInfo");
                        return false;
                    }

                    string name = FullServerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
                    m = Utilities.MediaInfoLib.MediaInfo.GetMediaInfo(name); //Mediainfo should have libcurl.dll for http
                    var duration = m?.GeneralStream?.Duration ?? 0;
                    if (duration == 0)
                        m = null;
                }


                if (m != null)
                {
                    SVR_VideoLocal info = VideoLocal;

                    List<TextStream> subs = SubtitleHelper.GetSubtitleStreams(this);
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
                logger.Error($"Unable to read the media information of file {FullServerPath ?? VideoLocal_Place_ID.ToString()} ERROR: {e}");
            }
            return false;
        }

        [Obsolete]
        public (bool, string) RemoveAndDeleteFile(bool deleteFolder = true)
        {
            // TODO Make this take an argument to disable removing empty dirs. It's slow, and should only be done if needed
            try
            {
                logger.Info("Deleting video local place record and file: {0}", (FullServerPath ?? VideoLocal_Place_ID.ToString()));
                
                if (!File.Exists(FullServerPath))
                {
                    logger.Info($"Unable to find file. Removing Record: {FullServerPath ?? FilePath}");
                    RemoveRecord();
                    return (true, string.Empty);
                }

                try
                {
                    File.Delete(FullServerPath);
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException)
                    {
                        if (deleteFolder) RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
                        RemoveRecord();
                        return (true, string.Empty);
                    }

                    logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                    return (false, $"Unable to delete file '{FullServerPath}'");
                }
                if (deleteFolder) RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
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
            logger.Info("Deleting video local place record and file: {0}", (FullServerPath ?? VideoLocal_Place_ID.ToString()));

            if (!File.Exists(FullServerPath))
            {
                logger.Info($"Unable to find file. Removing Record: {FullServerPath ?? FilePath}");
                RemoveRecord();
                return;
            }

            try
            {
                File.Delete(FullServerPath);
            }
            catch (FileNotFoundException)
            {
                if (deleteFolder) RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
                RemoveRecord();
                return;
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                throw;
            }
            if (deleteFolder) RecursiveDeleteEmptyDirectories(ImportFolder?.ImportFolderLocation, true);
            RemoveRecord();
        }

        public void RemoveAndDeleteFileWithOpenTransaction(ISession session, HashSet<SVR_AnimeSeries> seriesToUpdate)
        {
            // TODO Make this take an argument to disable removing empty dirs. It's slow, and should only be done if needed
            try
            {
                logger.Info("Deleting video local place record and file: {0}", (FullServerPath ?? VideoLocal_Place_ID.ToString()));

                
                if (!File.Exists(FullServerPath))
                {
                    logger.Info($"Unable to find file. Removing Record: {FullServerPath}");
                    RemoveRecordWithOpenTransaction(session, seriesToUpdate);
                    return;
                }

                try
                {
                    File.Delete(FullServerPath);
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
            bool invert = ServerSettings.Instance.Import.RenameThenMove;
            // Move first so that we don't bother the filesystem watcher
            bool succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
            if (!succeeded)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
                if (!succeeded)
                {
                    Thread.Sleep((int) DELAY_IN_USE.SECOND);
                    succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
                    if (!succeeded)
                    {
                        Thread.Sleep((int) DELAY_IN_USE.THIRD);
                        succeeded = invert ? RenameIfRequired() : MoveFileIfRequired();
                        if (!succeeded) return; // Don't bother renaming if we couldn't move. It'll need user interaction
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
                    Thread.Sleep((int) DELAY_IN_USE.SECOND);
                    succeeded = invert ? MoveFileIfRequired() : RenameIfRequired();
                    if (!succeeded)
                    {
                        Thread.Sleep((int) DELAY_IN_USE.THIRD);
                        succeeded = invert ? MoveFileIfRequired() : RenameIfRequired();
                        if (!succeeded) return;
                    }
                }
            }

            LinuxFS.SetLinuxPermissions(FullServerPath, ServerSettings.Instance.Linux.UID,
                ServerSettings.Instance.Linux.GID, ServerSettings.Instance.Linux.Permission);
        }

        // returns false if we should retry
        private bool RenameIfRequired()
        {
            if (!ServerSettings.Instance.Import.RenameOnImport)
            {
                logger.Trace($"Skipping rename of \"{this.FullServerPath}\" as rename on import is disabled");
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

            FileInfo sourceFile = new FileInfo(FullServerPath);

            // There is a possibility of weird logic based on source of the file. Some handling should be made for it....later
            (var destImpl, string newFolderPath) = RenameFileHelper.GetDestination(this, scriptName);

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
            SVR_ImportFolder dropFolder = ImportFolder;

            if (string.IsNullOrEmpty(newFolderPath))
            {
                logger.Error($"Unable to find destination for: \"{FullServerPath}\"");
                return (string.Empty, "ERROR: The returned path was null or empty");
            }

            // We've already resolved FullServerPath, so it doesn't need to be checked
            string newFilePath = Path.Combine(newFolderPath, Path.GetFileName(FullServerPath));
            string newFullServerPath = Path.Combine(destFolder.ImportFolderLocation, newFilePath);

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

            ShokoServer.PauseWatchingFiles();

            logger.Info($"Moving file from \"{FullServerPath}\" to \"{newFullServerPath}\"");
            try
            {
                sourceFile.MoveTo(newFullServerPath);
            }
            catch (Exception e)
            {
                logger.Error($"Unable to MOVE file: \"{FullServerPath}\" to \"{newFullServerPath}\" error {e}");
                ShokoServer.UnpauseWatchingFiles();
                return (newFullServerPath, "ERROR: " + e);
            }

            // Save for later. Scan for subtitles while the vlplace is still set for the source location
            string originalFileName = FullServerPath;
            var textStreams = SubtitleHelper.GetSubtitleStreams(this);

            // Handle Duplicate Files
            var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID).ToList();

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
                    RepoFactory.DuplicateFile.Save(dup);
                else
                    RepoFactory.DuplicateFile.Delete(dup);
            }

            ImportFolderID = destFolder.ImportFolderID;
            FilePath = newFilePath;
            RepoFactory.VideoLocalPlace.Save(this);

            try
            {
                // move any subtitle files
                foreach (TextStream subtitleFile in textStreams)
                {
                    if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;
                    var newParent = Path.GetDirectoryName(newFullServerPath);
                    var srcParent = Path.GetDirectoryName(originalFileName);
                    if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(srcParent)) continue;
                    var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                    if (!File.Exists(subPath)) continue;
                    var subFile = new FileInfo(subPath);
                    string newSubPath = Path.Combine(newParent, subFile.Name);
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

            // check for any empty folders in drop folder
            // only for the drop folder
            if (dropFolder.IsDropSource == 1)
            {
                RecursiveDeleteEmptyDirectories(dropFolder?.ImportFolderLocation, true);
            }
            ShokoServer.UnpauseWatchingFiles();
            return (newFolderPath, string.Empty);
        }

        // returns false if we should retry
        private bool MoveFileIfRequired(bool deleteEmpty = true)
        {
            // TODO move A LOT of this into renamer helper methods. A renamer can do them optionally
            if (!ServerSettings.Instance.Import.MoveOnImport)
            {
                logger.Trace($"Skipping move of \"{this.FullServerPath}\" as move on import is disabled");
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
                (var destImpl, string newFolderPath) = RenameFileHelper.GetDestination(this, null);

                if (!(destImpl is SVR_ImportFolder destFolder))
                {
                    // In this case, an error string was returned, but we'll suppress it and give an error elsewhere
                    if (newFolderPath != null) return true;
                    logger.Error($"Could not find a valid destination: \"{FullServerPath}\"");
                    return true;
                }

                // keep the original drop folder for later (take a copy, not a reference)
                SVR_ImportFolder dropFolder = ImportFolder;

                if (string.IsNullOrEmpty(newFolderPath))
                {
                    return true;
                }

                // We've already resolved FullServerPath, so it doesn't need to be checked
                string newFilePath = Path.Combine(newFolderPath, Path.GetFileName(FullServerPath));
                string newFullServerPath = Path.Combine(destFolder.ImportFolderLocation, newFilePath);

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
                        return true;
                    }
                }

                // Last ditch effort to ensure we aren't moving a file unto itself
                if (newFullServerPath.Equals(FullServerPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Error($"Resolved to move \"{newFullServerPath}\" unto itself. NOT MOVING");
                    return true;
                }

                var originalFileName = FullServerPath;
                var textStreams = SubtitleHelper.GetSubtitleStreams(this);
                
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
                        logger.Info($"Not moving file as it already exists at the new location, deleting source file instead: \"{FullServerPath}\" --- \"{newFullServerPath}\"");

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
                            if (dropFolder.IsDropSource != 1) return true;
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
                        (bool success, string _) = destVideoLocalPlace.RemoveAndDeleteFile();
                        if (!success) return false;

                        // Move
                        ShokoServer.PauseWatchingFiles();
                        logger.Info($"Moving file from \"{FullServerPath}\" to \"{newFullServerPath}\"");
                        try
                        {
                            sourceFile.MoveTo(newFullServerPath);
                        }
                        catch (Exception e)
                        {
                            logger.Error($"Unable to MOVE file: \"{FullServerPath}\" to \"{newFullServerPath}\" error {e}");
                            ShokoServer.UnpauseWatchingFiles();
                            return false;
                        }

                        // Handle Duplicate Files
                        var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID).ToList();

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
                                RepoFactory.DuplicateFile.Save(dup);
                            else
                                RepoFactory.DuplicateFile.Delete(dup);
                        }

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
                    ShokoServer.PauseWatchingFiles();
                    logger.Info($"Moving file from \"{FullServerPath}\" to \"{newFullServerPath}\"");
                    try
                    {
                        sourceFile.MoveTo(newFullServerPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to MOVE file: \"{FullServerPath}\" to \"{newFullServerPath}\" error {e}");
                        ShokoServer.UnpauseWatchingFiles();
                        return false;
                    }

                    // Handle Duplicate Files
                    var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID).ToList();

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
                            RepoFactory.DuplicateFile.Save(dup);
                        else
                            RepoFactory.DuplicateFile.Delete(dup);
                    }

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
                
                try
                {
                    // move any subtitle files
                    foreach (TextStream subtitleFile in textStreams)
                    {
                        if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;
                        var newParent = Path.GetDirectoryName(newFullServerPath);
                        var srcParent = Path.GetDirectoryName(originalFileName);
                        if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(srcParent)) continue;
                        var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                        if (!File.Exists(subPath)) continue;
                        var subFile = new FileInfo(subPath);
                        string newSubPath = Path.Combine(newParent, subFile.Name);
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
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not MOVE file: \"{FullServerPath ?? VideoLocal_Place_ID.ToString()}\" -- {ex}");
            }
            ShokoServer.UnpauseWatchingFiles();
            return true;
        }
        
        private void RecursiveDeleteEmptyDirectories(string dir, bool importfolder)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return;
                if (!Directory.Exists(dir)) return;
                if (IsDirectoryEmpty(dir))
                {
                    if (importfolder)
                        return;
                    try
                    {
                        Directory.Delete(dir);
                    }
                    catch (Exception ex)
                    {
                        if (ex is DirectoryNotFoundException || ex is FileNotFoundException)
                            return;
                        logger.Warn("Unable to DELETE directory: {0} Error: {1}", dir,
                            ex);
                    }
                    return;
                }

                // If it has folder, recurse
                foreach (string d in Directory.EnumerateDirectories(dir))
                    RecursiveDeleteEmptyDirectories(d, false);
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException || e is DirectoryNotFoundException)
                    return;
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

        string IVideoFile.Filename => Path.GetFileName(FilePath);
        string IVideoFile.FilePath => FullServerPath;
        long IVideoFile.FileSize => VideoLocal?.FileSize ?? 0;
        public IAniDBFile AniDBFileInfo => VideoLocal?.GetAniDBFile();

        public IHashes Hashes => VideoLocal == null
            ? null
            : new VideoHashes
                {CRC = VideoLocal.CRC32, MD5 = VideoLocal.MD5, ED2K = VideoLocal.Hash, SHA1 = VideoLocal.SHA1};

        public IMediaContainer MediaInfo => VideoLocal?.Media;

        private class VideoHashes : IHashes
        {
            public string CRC { get; set; }
            public string MD5 { get; set; }
            public string ED2K { get; set; }
            public string SHA1 { get; set; }
        }
    }
}
