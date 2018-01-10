using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nancy.Extensions;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Models.Azure;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper.MediaInfo;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Repos;
using Shoko.Server.Utilities;
using Stream = Shoko.Models.PlexAndKodi.Stream;

namespace Shoko.Server.Models
{
    public enum DELAY_IN_USE
    {
        FIRST = 750,
        SECOND = 3000,
        THIRD = 5000
    }

    public class SVR_VideoLocal_Place : VideoLocal_Place
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        internal SVR_ImportFolder ImportFolder => Repo.ImportFolder.GetByID(ImportFolderID);

        [NotMapped]
        public string FullServerPath
        {
            get
            {
                if (string.IsNullOrEmpty(ImportFolder?.ImportFolderLocation) || string.IsNullOrEmpty(FilePath))
                    return null;
                return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
            }
        }

        [NotMapped]
        public string FileName => Path.GetFileName(FilePath.Replace("\\", Path.DirectorySeparatorChar.ToString()).Replace("/", Path.DirectorySeparatorChar.ToString()));

        [NotMapped]
        public SVR_VideoLocal VideoLocal => Repo.VideoLocal.GetByID(VideoLocalID);

        // returns false if we should try again after the timer
        private bool RenameFile()
        {
            if (ImportFolder == null)
            {
                logger.Error($"Error: The renamer can't get the import folder for ImportFolderID: {ImportFolderID}, File: {FilePath}");
                return false;
            }

            IFileSystem filesys = ImportFolder.FileSystem;
            if (filesys == null)
            {
                logger.Error($"Error: The renamer can't get the filesystem for: {FullServerPath}");
                return true;
            }

            var renamer = RenameFileHelper.GetRenamer();
            if (renamer == null) return true;
            string renamed = renamer.GetFileName(this);
            if (string.IsNullOrEmpty(renamed))
            {
                logger.Error("Error: The renamer returned a null or empty name for: " + FilePath);
                return true;
            }

            if (renamed.StartsWith("*Error: "))
            {
                logger.Error("Error: The renamer returned an error on file: " + FilePath + "\n            " + renamed);
                return true;
            }

            // actually rename the file
            string fullFileName = FullServerPath;

            // check if the file exists
            if (string.IsNullOrEmpty(fullFileName))
            {
                logger.Error("Error could not find the original file for renaming, or it is in use: " + fullFileName);
                return false;
            }

            IObject file = filesys.Resolve(fullFileName);
            if (file.Status != Status.Ok)
            {
                logger.Error("Error could not find the original file for renaming, or it is in use: " + fullFileName);
                return false;
            }

            // actually rename the file
            string path = Path.GetDirectoryName(fullFileName);
            string newFullName = path == null ? null : Path.Combine(path, renamed);

            try
            {
                logger.Info($"Renaming file From ({fullFileName}) to ({newFullName})....");

                if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Info($"Renaming file SKIPPED! no change From ({fullFileName}) to ({newFullName})");
                    return true;
                }

                IObject r = file.FileSystem?.Resolve(newFullName);
                if (r==null || r.Status == Status.Ok)
                {
                    logger.Info($"Renaming file SKIPPED! Destination Exists ({newFullName})");
                    return true;
                }

                ShokoServer.StopWatchingFiles();

                FileSystemResult resu = file.Rename(renamed);
                if (resu.Status != Status.Ok)
                {
                    logger.Info($"Renaming file FAILED! From ({fullFileName}) to ({newFullName}) - {r.Error ?? "Result is null"}");
                    ShokoServer.StartWatchingFiles(false);
                    return false;
                }

                logger.Info($"Renaming file SUCCESS! From ({fullFileName}) to ({newFullName})");
                (SVR_ImportFolder, string) tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
                if (tup.Item1 == null)
                {
                    logger.Error($"Unable to LOCATE file {newFullName} inside the import folders");
                    ShokoServer.StartWatchingFiles(false);
                    return false;
                }

                // Before we change all references, remap Duplicate Files
                List<DuplicateFile> dups = Repo.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);
                if (dups != null && dups.Count > 0)
                {
                    foreach (var dup in dups)
                    {
                        using (var upd = Repo.DuplicateFile.BeginUpdate(dup))
                        {
                            bool dupchanged = false;
                            if (upd.Entity.FilePathFile1.Equals(FilePath, StringComparison.InvariantCultureIgnoreCase) && upd.Entity.ImportFolderIDFile1 == ImportFolderID)
                            {
                                upd.Entity.FilePathFile1 = tup.Item2;
                                dupchanged = true;
                            }
                            else if (upd.Entity.FilePathFile2.Equals(FilePath, StringComparison.InvariantCultureIgnoreCase) && upd.Entity.ImportFolderIDFile2 == ImportFolderID)
                            {
                                upd.Entity.FilePathFile2 = tup.Item2;
                                dupchanged = true;
                            }

                            if (dupchanged)
                                upd.Commit();
                        }
                    }
                }

                var filename_hash = Repo.FileNameHash.GetByHash(VideoLocal.Hash);
                if (!filename_hash.Any(a => a.FileName.Equals(renamed)))
                {
                    FileNameHash fnhash = new FileNameHash
                    {
                        DateTimeUpdated = DateTime.Now,
                        FileName = renamed,
                        FileSize = VideoLocal.FileSize,
                        Hash = VideoLocal.Hash
                    };
                    Repo.FileNameHash.BeginAdd(fnhash).Commit();
                }

                using (var vupd = Repo.VideoLocal_Place.BeginUpdate(this))
                {
                    vupd.Entity.FilePath = tup.Item2;
                    vupd.Commit();
                }
            }
            catch (Exception ex)
            {
                logger.Info($"Renaming file FAILED! From ({fullFileName}) to ({newFullName}) - {ex.Message}");
                logger.Error(ex, ex.ToString());
            }

            ShokoServer.StartWatchingFiles(false);
            return true;
        }

        public void RemoveRecord()
        {
            logger.Info("Removing VideoLocal_Place record for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
            List<SVR_AnimeEpisode> episodesToUpdate = new List<SVR_AnimeEpisode>();
            List<SVR_AnimeSeries> seriesToUpdate = new List<SVR_AnimeSeries>();
            SVR_VideoLocal v = VideoLocal;
            List<DuplicateFile> dupFiles = null;
            if (!string.IsNullOrEmpty(FilePath))
                dupFiles = Repo.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);

            if (v.Places.Count <= 1)
            {
                episodesToUpdate.AddRange(v.GetAnimeEpisodes());
                seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));

                Repo.VideoLocal_Place.Delete(this);
                Repo.VideoLocal.Delete(v);
                dupFiles?.ForEach(a => Repo.DuplicateFile.Delete(a));
                CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                cmdDel.Save();
            }
            else
            {
                Repo.VideoLocal_Place.Delete(this);
                dupFiles?.ForEach(a => Repo.DuplicateFile.Delete(a));
            }

            episodesToUpdate = episodesToUpdate.DistinctBy(a => a.AnimeEpisodeID).ToList();
            foreach (SVR_AnimeEpisode ep in episodesToUpdate)
            {
                try
                {
                    Repo.AnimeEpisode.BeginUpdate(ep).Commit();
                }
                catch (Exception ex)
                {
                    LogManager.GetCurrentClassLogger().Error(ex, ex.ToString());
                }
            }

            seriesToUpdate = seriesToUpdate.DistinctBy(a => a.AnimeSeriesID).ToList();
            foreach (SVR_AnimeSeries ser in seriesToUpdate)
            {
                ser.QueueUpdateStats();
            }
        }


        public void RemoveRecordWithOpenTransaction(HashSet<SVR_AnimeEpisode> episodesToUpdate, HashSet<SVR_AnimeSeries> seriesToUpdate)
        {
            logger.Info("Removing VideoLocal_Place recoord for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
            SVR_VideoLocal v = VideoLocal;

            List<DuplicateFile> dupFiles = null;
            if (!string.IsNullOrEmpty(FilePath))
                dupFiles = Repo.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID);

            if (v?.Places?.Count <= 1)
            {
                List<SVR_AnimeEpisode> eps = v.GetAnimeEpisodes()?.Where(a => a != null).ToList();
                eps?.ForEach(e=>episodesToUpdate.Add(e));
                eps?.Select(a => a.GetAnimeSeries()).ToList().ForEach(s=>seriesToUpdate.Add(s));
                Repo.VideoLocal_Place.Delete(this);
                Repo.VideoLocal.Delete(v);
                dupFiles?.ForEach(a => Repo.DuplicateFile.Delete(a));
                CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                cmdDel.Save();
            }
            else
            {
                Repo.VideoLocal_Place.Delete(this);
                dupFiles?.ForEach(a => Repo.DuplicateFile.Delete(a));
            }
        }

        public IFile GetFile()
        {
            IFileSystem fs = ImportFolder?.FileSystem;
            IObject fobj = fs?.Resolve(FullServerPath);
            if (fobj == null || fobj.Status != Status.Ok || fobj is IDirectory)
                return null;
            return fobj as IFile;
        }

        public async Task<IFile> GetFileAsync()
        {
            IFileSystem fs = ImportFolder?.FileSystem;
            if (fs == null)
                return null;
            IObject fobj = await fs.ResolveAsync(FullServerPath);
            if (fobj == null || fobj.Status != Status.Ok || fobj is IDirectory)
                return null;
            return fobj as IFile;
        }

        public static void FillVideoInfoFromMedia(SVR_VideoLocal info, Media m)
        {
            info.VideoResolution = !string.IsNullOrEmpty(m.Width) && !string.IsNullOrEmpty(m.Height) ? m.Width + "x" + m.Height : string.Empty;
            info.VideoCodec = !string.IsNullOrEmpty(m.VideoCodec) ? m.VideoCodec : m.Parts.SelectMany(a => a.Streams).FirstOrDefault(a => a.StreamType == "1")?.CodecID ?? string.Empty;
            info.AudioCodec = !string.IsNullOrEmpty(m.AudioCodec) ? m.AudioCodec : m.Parts.SelectMany(a => a.Streams).FirstOrDefault(a => a.StreamType == "2")?.CodecID ?? string.Empty;


            if (!string.IsNullOrEmpty(m.Duration))
            {
                bool isValidDuration = double.TryParse(m.Duration, out double _);
                if (isValidDuration)
                    info.Duration = (long) double.Parse(m.Duration, NumberStyles.Any, CultureInfo.InvariantCulture);
                else
                    info.Duration = 0;
            }
            else
                info.Duration = 0;

            info.VideoBitrate = info.VideoBitDepth = info.VideoFrameRate = info.AudioBitrate = string.Empty;
            List<Stream> vparts = m.Parts.SelectMany(a => a.Streams).Where(a => a.StreamType == "1").ToList();
            if (vparts.Count > 0)
            {
                if (!string.IsNullOrEmpty(vparts[0].Bitrate))
                    info.VideoBitrate = vparts[0].Bitrate;
                if (!string.IsNullOrEmpty(vparts[0].BitDepth))
                    info.VideoBitDepth = vparts[0].BitDepth;
                if (!string.IsNullOrEmpty(vparts[0].FrameRate))
                    info.VideoFrameRate = vparts[0].FrameRate;
            }

            List<Stream> aparts = m.Parts.SelectMany(a => a.Streams).Where(a => a.StreamType == "2").ToList();
            if (aparts.Count > 0)
            {
                if (!string.IsNullOrEmpty(aparts[0].Bitrate))
                    info.AudioBitrate = aparts[0].Bitrate;
            }
        }

        public bool RefreshMediaInfo(SVR_VideoLocal vl_ra)
        {
            try
            {
                logger.Trace("Getting media info for: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());
                Media m = null;
                List<Azure_Media> webmedias = AzureWebAPI.Get_Media(vl_ra.ED2KHash);
                if (webmedias != null && webmedias.Count > 0 && webmedias.FirstOrDefault(a => a != null) != null)
                {
                    m = webmedias.FirstOrDefault(a => a != null).ToMedia();
                }

                if (m == null && FullServerPath != null)
                {
                    if (GetFile() == null)
                    {
                        logger.Error($"File {FullServerPath ?? VideoLocal_Place_ID.ToString()} failed to be retrived for MediaInfo");
                        return false;
                    }

                    string name = ImportFolder.CloudID == null ? FullServerPath.Replace("/", $"{Path.DirectorySeparatorChar}") : ((IProvider) null).ReplaceSchemeHost(((IProvider) null).ConstructVideoLocalStream(0, VideoLocalID.ToString(), "file", false));
                    m = MediaConvert.Convert(name, GetFile()); //Mediainfo should have libcurl.dll for http
                    if (string.IsNullOrEmpty(m?.Duration))
                        m = null;
                    if (m != null)
                        AzureWebAPI.Send_Media(vl_ra.ED2KHash, m);
                }


                if (m != null)
                {
                    FillVideoInfoFromMedia(vl_ra, m);

                    m.Id = VideoLocalID.ToString();
                    List<Stream> subs = SubtitleHelper.GetSubtitleStreams(this);
                    if (subs.Count > 0)
                    {
                        m.Parts[0].Streams.AddRange(subs);
                    }

                    foreach (Part p in m.Parts)
                    {
                        p.Id = null;
                        p.Accessible = "1";
                        p.Exists = "1";
                        bool vid = false;
                        bool aud = false;
                        bool txt = false;
                        foreach (Stream ss in p.Streams.ToArray())
                        {
                            if (ss.StreamType == "1" && !vid) vid = true;
                            if (ss.StreamType == "2" && !aud)
                            {
                                aud = true;
                                ss.Selected = "1";
                            }

                            if (ss.StreamType == "3" && !txt)
                            {
                                txt = true;
                                ss.Selected = "1";
                            }
                        }
                    }

                    vl_ra.Media = m;             
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

        public bool RemoveAndDeleteFile()
        {
            try
            {
                logger.Info("Deleting video local place record and file: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());

                IFileSystem fileSystem = ImportFolder?.FileSystem;
                if (fileSystem == null)
                {
                    logger.Info("Unable to delete file, filesystem not found. Removing record.");
                    RemoveRecord();
                    return true;
                }

                if (FullServerPath == null)
                {
                    logger.Info("Unable to delete file, fullserverpath is null. Removing record.");
                    RemoveRecord();
                    return true;
                }

                IObject fr = fileSystem.Resolve(FullServerPath);
                if (fr.Status!=Status.Ok)
                {
                    logger.Info($"Unable to find file. Removing Record: {FullServerPath}");
                    RemoveRecord();
                    return true;
                }

                if (!(fr is IFile file))
                {
                    logger.Info($"Seems '{FullServerPath}' is a directory. Removing Record");
                    RemoveRecord();
                    return true;
                }

                IObject dd = fileSystem.Resolve(ImportFolder.ImportFolderLocation);
                try
                {
                    FileSystemResult fs = file.Delete(false);
                    if (fs.Status!=Status.Ok)
                    {
                        logger.Error($"Unable to delete file '{FullServerPath}': {fs.Error ?? "No Error Message"}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException)
                    {
                        if (dd.Status==Status.Ok && dd is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                        RemoveRecord();
                        return true;
                    }

                    logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                    return false;
                }

                if (dd.Status == Status.Ok && dd is IDirectory)
                    RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                RemoveRecord();
                // For deletion of files from Trakt, we will rely on the Daily sync
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }
        }

        public string RemoveAndDeleteFileWithMessage()
        {
            try
            {
                logger.Info("Deleting video local place record and file: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());

                IFileSystem fileSystem = ImportFolder?.FileSystem;
                if (fileSystem == null)
                {
                    logger.Info("Unable to delete file, filesystem not found. Removing record.");
                    RemoveRecord();
                    return string.Empty;
                }

                if (FullServerPath == null)
                {
                    logger.Info("Unable to delete file, fullserverpath is null. Removing record.");
                    RemoveRecord();
                    return string.Empty;
                }

                IObject fr = fileSystem.Resolve(FullServerPath);
                if (fr.Status!=Status.Ok)
                {
                    logger.Info($"Unable to find file. Removing Record: {FullServerPath}");
                    RemoveRecord();
                    return string.Empty;
                }

                if (!(fr is IFile file))
                {
                    logger.Info($"Seems '{FullServerPath}' is a directory. Removing Record");
                    RemoveRecord();
                    return string.Empty;
                }

                IObject dd = fileSystem.Resolve(ImportFolder.ImportFolderLocation);
                try
                {
                    FileSystemResult fs = file.Delete(false);
                    if (fs.Status!=Status.Ok)
                    {
                        logger.Error($"Unable to delete file '{FullServerPath}': {fs.Error ?? "No Error Message"}");
                        return $"Unable to delete file '{FullServerPath}'";
                    }
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException)
                    {
                        if (dd.Status==Status.Ok && dd is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                        RemoveRecord();
                        return string.Empty;
                    }

                    logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                    return $"Unable to delete file '{FullServerPath}'";
                }

                if (dd.Status==Status.Ok && dd is IDirectory)
                    RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                RemoveRecord();
                // For deletion of files from Trakt, we will rely on the Daily sync
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public void RemoveAndDeleteFileWithOpenTransaction(HashSet<SVR_AnimeEpisode> episodesToUpdate, HashSet<SVR_AnimeSeries> seriesToUpdate)
        {
            try
            {
                logger.Info("Deleting video local place record and file: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());

                IFileSystem fileSystem = ImportFolder?.FileSystem;
                if (fileSystem == null)
                {
                    logger.Info("Unable to delete file, filesystem not found. Removing record.");
                    RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                    return;
                }

                if (FullServerPath == null)
                {
                    logger.Info("Unable to delete file, fullserverpath is null. Removing record.");
                    RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                    return;
                }

                IObject fr = fileSystem.Resolve(FullServerPath);
                if (fr.Status!=Status.Ok)
                {
                    logger.Info($"Unable to find file. Removing Record: {FullServerPath}");
                    RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                    return;
                }

                if (!(fr is IFile file))
                {
                    logger.Info($"Seems '{FullServerPath}' is a directory. Removing Record");
                    RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                    return;
                }

                IObject dd = fileSystem.Resolve(ImportFolder.ImportFolderLocation);
                try
                {
                    FileSystemResult fs = file.Delete(false);
                    if (fs.Status!=Status.Ok)
                    {
                        logger.Error($"Unable to delete file '{FullServerPath}': {fs.Error ?? "No Error Message"}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException)
                    {
                        if (dd.Status==Status.Ok && dd is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                        RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                        return;
                    }

                    logger.Error($"Unable to delete file '{FullServerPath}': {ex}");
                    return;
                }

                if (dd.Status==Status.Ok && dd is IDirectory)
                    RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                // For deletion of files from Trakt, we will rely on the Daily sync
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void RenameAndMoveAsRequired()
        {
            bool succeeded = RenameIfRequired();
            if (!succeeded)
            {
                Thread.Sleep((int) DELAY_IN_USE.FIRST);
                succeeded = RenameIfRequired();
                if (!succeeded)
                {
                    Thread.Sleep((int) DELAY_IN_USE.SECOND);
                    succeeded = RenameIfRequired();
                    if (!succeeded)
                    {
                        Thread.Sleep((int) DELAY_IN_USE.THIRD);
                        succeeded = RenameIfRequired();
                        if (!succeeded)
                        {
                            // Don't bother moving if we can't rename
                            return;
                        }
                    }
                }
            }

            succeeded = MoveFileIfRequired();
            if (!succeeded)
            {
                Thread.Sleep((int) DELAY_IN_USE.FIRST);
                succeeded = MoveFileIfRequired();
                if (!succeeded)
                {
                    Thread.Sleep((int) DELAY_IN_USE.SECOND);
                    succeeded = MoveFileIfRequired();
                    if (!succeeded)
                    {
                        Thread.Sleep((int) DELAY_IN_USE.THIRD);
                        succeeded = MoveFileIfRequired();
                        if (!succeeded) return; //Same as above, but linux permissiosn.
                    }
                }
            }

            LinuxFS.SetLinuxPermissions(FullServerPath, ServerSettings.Linux_UID, ServerSettings.Linux_GID, ServerSettings.Linux_Permission);
        }

        // returns false if we should retry
        private bool RenameIfRequired()
        {
            try
            {
                return RenameFile();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return true;
            }
        }

        public string MoveWithResultString(IObject fileSystemResult, string scriptName, bool force = false)
        {
            if (FullServerPath == null)
            {
                logger.Error("Could not find or access the file to move: {0}", VideoLocal_Place_ID);
                return "ERROR: Unable to access file";
            }

            // check if this file is in the drop folder
            // otherwise we don't need to move it
            if (ImportFolder.IsDropSource == 0 && !force)
            {
                logger.Error("Not moving file as it is NOT in the drop folder: {0}", FullServerPath);
                return "ERROR: Not in drop folder";
            }

            IFile source_file = fileSystemResult as IFile;
            // We checked the above prior, so no error checking

            // There is a possibilty of weird logic based on source of the file. Some handling should be made for it....later
            (var destImpl, string newFolderPath) = RenameFileHelper.GetRenamer(scriptName).GetDestinationFolder(this);

            if (!(destImpl is SVR_ImportFolder destFolder))
            {
                // In this case, an error string was returned, but we'll suppress it and give an error elsewhere
                if (newFolderPath != null)
                {
                    logger.Error("Unable to find destination for: {0}", FullServerPath);
                    logger.Error("The error message was: " + newFolderPath);
                    return "ERROR: " + newFolderPath;
                }

                logger.Error("Unable to find destination for: {0}", FullServerPath);
                return "ERROR: There was an error but no error code returned...";
            }

            // keep the original drop folder for later (take a copy, not a reference)
            SVR_ImportFolder dropFolder = ImportFolder;

            if (string.IsNullOrEmpty(newFolderPath))
            {
                logger.Error("Unable to find destination for: {0}", FullServerPath);
                return "ERROR: The returned path was null or empty";
            }

            // We've already resolved FullServerPath, so it doesn't need to be checked
            string newFilePath = Path.Combine(newFolderPath, Path.GetFileName(FullServerPath));
            string newFullServerPath = Path.Combine(destFolder.ImportFolderLocation, newFilePath);

            IDirectory destination;

            fileSystemResult = destFolder.FileSystem.Resolve(Path.Combine(destFolder.ImportFolderLocation, newFolderPath));
            if (fileSystemResult.Status==Status.Ok)
            {
                destination = fileSystemResult as IDirectory;
            }
            else
            {
                //validate the directory tree.
                destination = destFolder.BaseDirectory;
                {
                    var dir = Path.GetDirectoryName(newFilePath);
                    if (dir != null)
                    {
                        foreach (var part in dir.Split(Path.DirectorySeparatorChar))
                        {
                            var wD = destination.Directories.FirstOrDefault(d => d.Name == part);
                            if (wD == null)
                            {
                                var result = destination.CreateDirectory(part, null);
                                if (result.Status != Status.Ok)
                                {
                                    logger.Error($"Unable to create directory {part} in {destination.FullName}: {result.Error}");
                                    return $"ERROR: Unable to create directory {part} in {destination.FullName}: {result.Error}";
                                }

                                destination = result;
                                continue;
                            }

                            destination = wD;
                        }
                    }
                }
            }


            // Last ditch effort to ensure we aren't moving a file unto itself
            if (newFullServerPath.Equals(FullServerPath, StringComparison.InvariantCultureIgnoreCase))
            {
                logger.Info($"The file is already at its desired location: {FullServerPath}");
                return "ERROR: The file is already at its desired location";
            }

            IFileSystem f = dropFolder.FileSystem;
            IObject dst = f.Resolve(newFullServerPath);
            if (dst.Status==Status.Ok)
            {
                logger.Info($"A file already exists at the desired location for {FullServerPath}");
                return "ERROR: The File already exists at the destination";
            }

            ShokoServer.StopWatchingFiles();

            logger.Info("Moving file from {0} to {1}", FullServerPath, newFullServerPath);
            FileSystemResult fr = source_file.Move(destination);
            if (fr.Status!=Status.Ok)
            {
                logger.Error("Unable to MOVE file: {0} to {1} error {2}", FullServerPath, newFullServerPath, fr.Error ?? "No Error String");
                ShokoServer.StartWatchingFiles(false);
                return "ERROR: " + (fr.Error ?? "Error moving file, but no error string");
            }

            string originalFileName = FullServerPath;

            // Handle Duplicate Files
            var dups = Repo.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID).ToList();

            foreach (var dup in dups)
            {
                bool delete = true;
                using (var upd = Repo.DuplicateFile.BeginUpdate(dup))
                {
                    // Move source
                    if (upd.Entity.FilePathFile1.Equals(FilePath) && upd.Entity.ImportFolderIDFile1 == ImportFolderID)
                    {
                        upd.Entity.FilePathFile1 = newFilePath;
                        upd.Entity.ImportFolderIDFile1 = destFolder.ImportFolderID;
                    }
                    else if (upd.Entity.FilePathFile2.Equals(FilePath) && upd.Entity.ImportFolderIDFile2 == ImportFolderID)
                    {
                        upd.Entity.FilePathFile2 = newFilePath;
                        upd.Entity.ImportFolderIDFile2 = destFolder.ImportFolderID;
                    }

                    if (!upd.Entity.GetFullServerPath1().Equals(upd.Entity.GetFullServerPath2(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        upd.Commit();
                        delete = false;
                    }
                }
                if (delete)
                    Repo.DuplicateFile.Delete(dup);
            }

            using (var up = Repo.VideoLocal_Place.BeginUpdate(this))
            {
                up.Entity.ImportFolderID = destFolder.ImportFolderID;
                up.Entity.FilePath = newFilePath;
                up.Commit();
            }
            try
            {
                // move any subtitle files
                foreach (string subtitleFile in Utils.GetPossibleSubtitleFiles(originalFileName))
                {
                    IObject src = f.Resolve(subtitleFile);
                    if (src.Status!=Status.Ok || !(src is IFile)) continue;
                    string dir = Path.GetDirectoryName(newFullServerPath);
                    if (dir == null)
                    {
                        logger.Warn("Unable to MOVE file: {0} Empty Path", subtitleFile);
                    }
                    else
                    {
                        string newSubPath = Path.Combine(dir, ((IFile)src).Name);
                        dst = f.Resolve(newSubPath);
                        if (dst.Status == Status.Ok && (dst is IFile))
                        {
                            FileSystemResult fr2 = src.Delete(false);
                            if (fr2.Status != Status.Ok)
                            {
                                logger.Warn("Unable to DELETE file: {0} error {1}", subtitleFile, fr2.Error ?? string.Empty);
                            }
                        }
                        else
                        {
                            FileSystemResult fr2 = ((IFile)src).Move(destination);
                            if (fr2.Status != Status.Ok)
                            {
                                logger.Error("Unable to MOVE file: {0} to {1} error {2}", subtitleFile, newSubPath, fr2.Error ?? string.Empty);
                            }
                        }
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
                IObject dd = f.Resolve(dropFolder.ImportFolderLocation);
                if (dd.Status==Status.Ok && dd is IDirectory)
                    RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
            }

            ShokoServer.StartWatchingFiles(false);
            return newFolderPath;
        }

        // returns false if we should retry
        private bool MoveFileIfRequired()
        {
            try
            {
                logger.Trace("Attempting to MOVE file: {0}", FullServerPath ?? VideoLocal_Place_ID.ToString());

                if (FullServerPath == null)
                {
                    logger.Error("Could not find or access the file to move: {0}", VideoLocal_Place_ID);
                    return true;
                }

                // check if this file is in the drop folder
                // otherwise we don't need to move it
                if (ImportFolder.IsDropSource == 0)
                {
                    logger.Trace("Not moving file as it is NOT in the drop folder: {0}", FullServerPath);
                    return true;
                }

                IFileSystem f = ImportFolder.FileSystem;
                if (f == null)
                {
                    logger.Trace("Unable to MOVE, filesystem not working: {0}", FullServerPath);
                    return true;
                }

                IObject fsrresult = f.Resolve(FullServerPath);
                if (fsrresult.Status!=Status.Ok)
                {
                    logger.Error("Could not find or access the file to move: {0}", FullServerPath);
                    // this can happen due to file locks, so retry
                    return false;
                }

                IFile source_file = fsrresult as IFile;
                if (source_file == null)
                {
                    logger.Error("Could not move the file (it isn't a file): {0}", FullServerPath);
                    // this means it isn't a file, but something else, so don't retry
                    return true;
                }

                // find the default destination
                (var destImpl, string newFolderPath) = RenameFileHelper.GetRenamerWithFallback()?.GetDestinationFolder(this) ?? (null, null);

                if (!(destImpl is SVR_ImportFolder destFolder))
                {
                    // In this case, an error string was returned, but we'll suppress it and give an error elsewhere
                    if (newFolderPath != null) return true;
                    logger.Error("Could not find a valid destination: {0}", FullServerPath);
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

                IDirectory destination;

                fsrresult = destFolder.FileSystem.Resolve(Path.Combine(destFolder.ImportFolderLocation, newFolderPath));
                if (fsrresult.Status==Status.Ok)
                {
                    destination = fsrresult as IDirectory;
                }
                else
                {
                    //validate the directory tree.
                    destination = destFolder.BaseDirectory;
                    {
                        var dir = Path.GetDirectoryName(newFilePath);
                        if (dir != null)
                        {
                            foreach (var part in dir.Split(Path.DirectorySeparatorChar))
                            {
                                var wD = destination.Directories.FirstOrDefault(d => d.Name == part);
                                if (wD == null)
                                {
                                    var result = destination.CreateDirectory(part, null);
                                    if (result.Status != Status.Ok)
                                    {
                                        logger.Error($"Unable to create directory {part} in {destination.FullName}: {result.Error}");
                                        return true;
                                    }

                                    destination = result;
                                    continue;
                                }

                                destination = wD;
                            }
                        }
                    }
                }


                // Last ditch effort to ensure we aren't moving a file unto itself
                if (newFullServerPath.Equals(FullServerPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Error($"Resolved to move {newFullServerPath} unto itself. NOT MOVING");
                    return true;
                }

                IObject dst = f.Resolve(newFullServerPath);
                if (dst.Status==Status.Ok)
                {
                    logger.Info("Not moving file as it already exists at the new location, deleting source file instead: {0} --- {1}", FullServerPath, newFullServerPath);

                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    FileSystemResult fr = new FileSystemResult();
                    try
                    {
                        fr = source_file.Delete(false);
                        if (fr.Status!=Status.Ok)
                        {
                            logger.Warn("Unable to DELETE file: {0} error {1}", FullServerPath, fr.Error ?? string.Empty);
                            return false;
                        }

                        RemoveRecord();

                        // check for any empty folders in drop folder
                        // only for the drop folder
                        if (dropFolder.IsDropSource != 1) return true;
                        IObject dd = f.Resolve(dropFolder.ImportFolderLocation);
                        if (dd.Status==Status.Ok && dd is IDirectory)
                        {
                            RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                        }

                        return true;
                    }
                    catch
                    {
                        logger.Error("Unable to DELETE file: {0} error {1}", FullServerPath, fr?.Error ?? string.Empty);
                        return true;
                    }
                }
                else
                {
                    ShokoServer.StopWatchingFiles();
                    logger.Info("Moving file from {0} to {1}", FullServerPath, newFullServerPath);
                    FileSystemResult fr = source_file.Move(destination);
                    if (fr.Status!=Status.Ok)
                    {
                        logger.Error("Unable to MOVE file: {0} to {1} error {2}", FullServerPath, newFullServerPath, fr.Error ?? "No Error String");
                        ShokoServer.StartWatchingFiles(false);
                        return false;
                    }

                    string originalFileName = FullServerPath;

                    // Handle Duplicate Files
                    var dups = Repo.DuplicateFile.GetByFilePathAndImportFolder(FilePath, ImportFolderID).ToList();

                    foreach (var dup in dups)
                    {
                        bool delete = true;
                        using (var upd = Repo.DuplicateFile.BeginUpdate(dup))
                        {
                            // Move source
                            if (upd.Entity.FilePathFile1.Equals(FilePath) && upd.Entity.ImportFolderIDFile1 == ImportFolderID)
                            {
                                upd.Entity.FilePathFile1 = newFilePath;
                                upd.Entity.ImportFolderIDFile1 = destFolder.ImportFolderID;
                            }
                            else if (upd.Entity.FilePathFile2.Equals(FilePath) && upd.Entity.ImportFolderIDFile2 == ImportFolderID)
                            {
                                upd.Entity.FilePathFile2 = newFilePath;
                                upd.Entity.ImportFolderIDFile2 = destFolder.ImportFolderID;
                            }

                            if (!upd.Entity.GetFullServerPath1().Equals(upd.Entity.GetFullServerPath2(), StringComparison.InvariantCultureIgnoreCase))
                            {
                                upd.Commit();
                                delete = false;
                            }
                        }
                        if (delete)
                            Repo.DuplicateFile.Delete(dup);
                    }

                    using (var up = Repo.VideoLocal_Place.BeginUpdate(this))
                    {
                        up.Entity.ImportFolderID = destFolder.ImportFolderID;
                        up.Entity.FilePath = newFilePath;
                        up.Commit();
                    }

                    try
                    {
                        // move any subtitle files
                        foreach (string subtitleFile in Utils.GetPossibleSubtitleFiles(originalFileName))
                        {
                            IObject src = f.Resolve(subtitleFile);
                            if (src.Status!=Status.Ok || !(src is IFile)) continue;
                            string dir = Path.GetDirectoryName(newFullServerPath);
                            if (dir == null)
                            {
                                logger.Warn("Unable to MOVE file: {0} Empty Path", subtitleFile);
                            }
                            else
                            {
                                string newSubPath = Path.Combine(dir, ((IFile)src).Name);
                                dst = f.Resolve(newSubPath);
                                if (dst.Status == Status.Ok && dst is IFile)
                                {
                                    FileSystemResult fr2 = src.Delete(false);
                                    if (fr2.Status != Status.Ok)
                                    {
                                        logger.Warn("Unable to DELETE file: {0} error {1}", subtitleFile, fr2.Error ?? string.Empty);
                                    }
                                }
                                else
                                {
                                    FileSystemResult fr2 = ((IFile)src).Move(destination);
                                    if (fr2.Status != Status.Ok)
                                    {
                                        logger.Error("Unable to MOVE file: {0} to {1} error {2}", subtitleFile, newSubPath, fr2.Error ?? string.Empty);
                                    }
                                }
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
                        IObject dd = f.Resolve(dropFolder.ImportFolderLocation);
                        if (dd.Status == Status.Ok && dd is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory) dd, true);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Could not MOVE file: {FullServerPath ?? VideoLocal_Place_ID.ToString()} -- {ex}";
                logger.Error(ex, msg);
            }

            ShokoServer.StartWatchingFiles(false);
            return true;
        }

        private void RecursiveDeleteEmptyDirectories(IDirectory dir, bool importfolder)
        {
            try
            {
                FileSystemResult fr = dir.Populate();
                if (fr.Status==Status.Ok)
                {
                    if (dir.Files.Count > 0 && dir.Directories.Count == 0)
                        return;
                    foreach (IDirectory d in dir.Directories)
                        RecursiveDeleteEmptyDirectories(d, false);
                }

                if (importfolder)
                    return;
                fr = dir.Populate();
                if (fr.Status==Status.Ok)
                {
                    if (dir.Files.Count == 0 && dir.Directories.Count == 0)
                    {
                        fr = dir.Delete(true);
                        if (fr.Status!=Status.Ok)
                        {
                            logger.Warn("Unable to DELETE directory: {0} error {1}", dir.FullName, fr.Error ?? string.Empty);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                    return;
                logger.Error($"There was an error removing the empty directory: {dir.FullName}\r\n{e}");
            }
        }
    }
}