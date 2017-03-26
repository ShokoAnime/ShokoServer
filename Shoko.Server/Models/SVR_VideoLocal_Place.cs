using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Models.Azure;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Nancy.Extensions;
using Shoko.Server.Commands;
using NHibernate;
using Shoko.Server.Databases;
using NLog;
using Shoko.Server.Extensions;
using NutzCode.CloudFileSystem;
using Shoko.Server.FileHelper.MediaInfo;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Media = Shoko.Models.PlexAndKodi.Media;

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
        public SVR_VideoLocal_Place()
        {
        }

        internal SVR_ImportFolder ImportFolder => RepoFactory.ImportFolder.GetByID(ImportFolderID);

        internal string FullServerPath
        {
            get
            {
                if (string.IsNullOrEmpty(ImportFolder?.ImportFolderLocation) || string.IsNullOrEmpty(FilePath))
                    return null;
                return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
            }
        }

        internal SVR_VideoLocal VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

        private static Logger logger = LogManager.GetCurrentClassLogger();

        // returns false if we should try again after the timer
        private bool RenameFile(string renameScript)
        {
            string renamed = RenameFileHelper.GetNewFileName(VideoLocal, renameScript);
            if (string.IsNullOrEmpty(renamed)) return true;

            IFileSystem filesys = ImportFolder?.FileSystem;
            if (filesys == null)
                return true;
            // actually rename the file
            string fullFileName = this.FullServerPath;

            // check if the file exists

            FileSystemResult<IObject> re = filesys.Resolve(fullFileName);
            if ((re == null) || (!re.IsOk))
            {
                logger.Error("Error could not find the original file for renaming: " + fullFileName);
                return false;
            }
            IObject file = re.Result;
            // actually rename the file
            string path = Path.GetDirectoryName(fullFileName);
            string newFullName = (path == null ? null : Path.Combine(path, renamed));

            try
            {
                logger.Info($"Renaming file From ({fullFileName}) to ({newFullName})....");

                if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Info($"Renaming file SKIPPED! no change From ({fullFileName}) to ({newFullName})");
                    return true;
                }

                FileSystemResult r = file?.FileSystem?.Resolve(newFullName);
                if (r != null && r.IsOk)
                {
                    logger.Info($"Renaming file SKIPPED! Destination Exists ({newFullName})");
                    return true;
                }

                r = file.Rename(renamed);
                if (r == null || !r.IsOk)
                {
                    logger.Info(
                        $"Renaming file FAILED! From ({fullFileName}) to ({newFullName}) - {r?.Error ?? "Result is null"}");
                    return false;
                }

                logger.Info($"Renaming file SUCCESS! From ({fullFileName}) to ({newFullName})");
                Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
                if (tup == null)
                {
                    logger.Error($"Unable to LOCATE file {newFullName} inside the import folders");
                    return false;
                }
                this.FilePath = tup.Item2;
                RepoFactory.VideoLocalPlace.Save(this);
            }
            catch (Exception ex)
            {
                logger.Info($"Renaming file FAILED! From ({fullFileName}) to ({newFullName}) - {ex.Message}");
                logger.Error(ex, ex.ToString());
            }
            return true;
        }

        public void RemoveRecord()
        {
            logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", FullServerPath);
            List<SVR_AnimeEpisode> episodesToUpdate = new List<SVR_AnimeEpisode>();
            List<SVR_AnimeSeries> seriesToUpdate = new List<SVR_AnimeSeries>();
            SVR_VideoLocal v = VideoLocal;
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                if (v.Places.Count <= 1)
                {
                    episodesToUpdate.AddRange(v.GetAnimeEpisodes());
                    seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));
                    using (var transaction = session.BeginTransaction())
                    {
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
                        transaction.Commit();
                    }
                    CommandRequest_DeleteFileFromMyList cmdDel =
                        new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                    cmdDel.Save();
                }
                else
                {
                    episodesToUpdate.AddRange(v.GetAnimeEpisodes());
                    seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));
                    using (var transaction = session.BeginTransaction())
                    {
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                        transaction.Commit();
                    }
                }
            }
            episodesToUpdate = episodesToUpdate.DistinctBy(a => a.AnimeEpisodeID).ToList();
            foreach (SVR_AnimeEpisode ep in episodesToUpdate)
            {
                if (ep.AnimeEpisodeID == 0)
                {
                    ep.PlexContract = null;
                    RepoFactory.AnimeEpisode.Save(ep);
                }
                try
                {
                    ep.PlexContract = Helper.GenerateVideoFromAnimeEpisode(ep);
                    RepoFactory.AnimeEpisode.Save(ep);
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


        public void RemoveRecordWithOpenTransaction(ISession session, ICollection<SVR_AnimeEpisode> episodesToUpdate,
            ICollection<SVR_AnimeSeries> seriesToUpdate)
        {
            logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", FullServerPath);
            SVR_VideoLocal v = VideoLocal;
            if (v.Places.Count <= 1)
            {
                v.GetAnimeEpisodes().ForEach(a => episodesToUpdate.Add(a));
                v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).ToList().ForEach(a => seriesToUpdate.Add(a));
                using (var transaction = session.BeginTransaction())
                {
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                    RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
                    transaction.Commit();
                }
                CommandRequest_DeleteFileFromMyList cmdDel =
                    new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                cmdDel.Save();
            }
            else
            {
                v.GetAnimeEpisodes().ForEach(a => episodesToUpdate.Add(a));
                v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).ToList().ForEach(a => seriesToUpdate.Add(a));
                using (var transaction = session.BeginTransaction())
                {
                    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
                    transaction.Commit();
                }
            }
        }

        public IFile GetFile()
        {
            IFileSystem fs = ImportFolder.FileSystem;
            if (fs == null)
                return null;
            FileSystemResult<IObject> fobj = fs.Resolve(FullServerPath);
            if (!fobj.IsOk || fobj.Result is IDirectory)
                return null;
            return fobj.Result as IFile;
        }

        public static void FillVideoInfoFromMedia(SVR_VideoLocal info, Media m)
        {
            info.VideoResolution = (!string.IsNullOrEmpty(m.Width) && !string.IsNullOrEmpty(m.Height))
                ? m.Width + "x" + m.Height
                : string.Empty;
            info.VideoCodec = (!string.IsNullOrEmpty(m.VideoCodec)) ? m.VideoCodec : string.Empty;
            info.AudioCodec = (!string.IsNullOrEmpty(m.AudioCodec)) ? m.AudioCodec : string.Empty;


            if (!string.IsNullOrEmpty(m.Duration))
            {
                double duration;
                bool isValidDuration = double.TryParse(m.Duration, out duration);
                if (isValidDuration)
                    info.Duration =
                        (long) double.Parse(m.Duration, NumberStyles.Any, CultureInfo.InvariantCulture);
                else
                    info.Duration = 0;
            }
            else
                info.Duration = 0;

            info.VideoBitrate = info.VideoBitDepth = info.VideoFrameRate = info.AudioBitrate = string.Empty;
            List<Shoko.Models.PlexAndKodi.Stream> vparts = m.Parts[0].Streams.Where(a => a.StreamType == "1").ToList();
            if (vparts.Count > 0)
            {
                if (!string.IsNullOrEmpty(vparts[0].Bitrate))
                    info.VideoBitrate = vparts[0].Bitrate;
                if (!string.IsNullOrEmpty(vparts[0].BitDepth))
                    info.VideoBitDepth = vparts[0].BitDepth;
                if (!string.IsNullOrEmpty(vparts[0].FrameRate))
                    info.VideoFrameRate = vparts[0].FrameRate;
            }
            List<Shoko.Models.PlexAndKodi.Stream> aparts = m.Parts[0].Streams.Where(a => a.StreamType == "2").ToList();
            if (aparts.Count > 0)
            {
                if (!string.IsNullOrEmpty(aparts[0].Bitrate))
                    info.AudioBitrate = aparts[0].Bitrate;
            }
        }

        public bool RefreshMediaInfo()
        {
            try
            {
                logger.Trace($"Getting media info for: {FullServerPath}");
                Media m = null;
                List<Shoko.Models.Azure.Azure_Media> webmedias = AzureWebAPI.Get_Media(VideoLocal.ED2KHash);
                if (webmedias != null && webmedias.Count > 0)
                {
                    m = webmedias[0].ToMedia();
                }
                if (m == null)
                {
                    string name = (ImportFolder.CloudID == null)
                        ? FullServerPath.Replace("/", "\\")
                        : ((IProvider) null).ReplaceSchemeHost(((IProvider) null).ConstructVideoLocalStream(0,
                            VideoLocalID.ToString(), "file", false));
                    m = MediaConvert.Convert(name, GetFile()); //Mediainfo should have libcurl.dll for http
                    if (string.IsNullOrEmpty(m.Duration))
                        m = null;
                    if (m != null)
                        AzureWebAPI.Send_Media(VideoLocal.ED2KHash, m);
                }


                if (m != null)
                {
                    SVR_VideoLocal info = VideoLocal;
                    FillVideoInfoFromMedia(info, m);

                    m.Id = VideoLocalID.ToString();
                    List<Shoko.Models.PlexAndKodi.Stream> subs = SubtitleHelper.GetSubtitleStreams(this);
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
                        foreach (Shoko.Models.PlexAndKodi.Stream ss in p.Streams.ToArray())
                        {
                            if ((ss.StreamType == "1") && !vid)
                            {
                                vid = true;
                            }
                            if ((ss.StreamType == "2") && !aud)
                            {
                                aud = true;
                                ss.Selected = "1";
                            }
                            if ((ss.StreamType == "3") && !txt)
                            {
                                txt = true;
                                ss.Selected = "1";
                            }
                        }
                    }
                    info.Media = m;
                    return true;
                }
                logger.Error($"File {FullServerPath} does not exist, unable to read media information from it");
            }
            catch (Exception e)
            {
                logger.Error($"Unable to read the media information of file {FullServerPath} ERROR: {e}");
            }
            return false;
        }

        public void RenameAndMoveAsRequired()
        {
            bool retry = RenameIfRequired();
            if (!retry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                retry = RenameIfRequired();
                if (!retry)
                {
                    Thread.Sleep((int) DELAY_IN_USE.SECOND);
                    retry = RenameIfRequired();
                    if (!retry)
                    {
                        Thread.Sleep((int) DELAY_IN_USE.THIRD);
                        retry = RenameIfRequired();
                        if (!retry)
                        {
                            // Don't bother moving if we can't rename
                            return;
                        }
                    }
                }
            }
            retry = MoveFileIfRequired();
            if (retry) return;
            Thread.Sleep((int)DELAY_IN_USE.FIRST);
            retry = MoveFileIfRequired();
            if (retry) return;
            Thread.Sleep((int) DELAY_IN_USE.SECOND);
            retry = MoveFileIfRequired();
            if (retry) return;
            Thread.Sleep((int) DELAY_IN_USE.THIRD);
            MoveFileIfRequired();
        }

        // returns false if we should retry
        private bool RenameIfRequired()
        {
            try
            {
                RenameScript defaultScript = RepoFactory.RenameScript.GetDefaultScript();

                return defaultScript == null || RenameFile(defaultScript.Script);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return true;
            }
        }

        // returns false if we should retry
        private bool MoveFileIfRequired()
        {
            try
            {
                logger.Trace("Attempting to MOVE file: {0}", this.FullServerPath);

                // check if this file is in the drop folder
                // otherwise we don't need to move it
                if (ImportFolder.IsDropSource == 0)
                {
                    logger.Trace("Not moving file as it is NOT in the drop folder: {0}", this.FullServerPath);
                    return true;
                }
                IFileSystem f = this.ImportFolder.FileSystem;
                if (f == null)
                {
                    logger.Trace("Unable to MOVE, filesystem not working: {0}", this.FullServerPath);
                    return true;
                }

                FileSystemResult<IObject> fsrresult = f.Resolve(FullServerPath);
                if (!fsrresult.IsOk)
                {
                    logger.Error("Could not find or access the file to move: {0}", this.FullServerPath);
                    // this can happen due to file locks, so retry
                    return false;
                }
                IFile source_file = fsrresult.Result as IFile;
                if (source_file == null)
                {
                    logger.Error("Could not move the file (it isn't a file): {0}", this.FullServerPath);
                    // this means it isn't a file, but something else, so don't retry
                    return true;
                }
                // find the default destination
                SVR_ImportFolder destFolder = null;
                foreach (SVR_ImportFolder fldr in RepoFactory.ImportFolder.GetAll()
                    .Where(a => a != null && a.CloudID == ImportFolder.CloudID).ToList())
                {
                    if (!fldr.FolderIsDropDestination) continue;
                    if (fldr.FolderIsDropSource) continue;
                    IFileSystem fs = fldr.FileSystem;
                    FileSystemResult<IObject> fsresult = fs?.Resolve(fldr.ImportFolderLocation);
                    if (fsresult == null || !fsresult.IsOk) continue;

                    string tempNewPath = Path.Combine(fldr.ImportFolderLocation, FilePath);
                    fsresult = fs.Resolve(tempNewPath);
                    if (fsresult.IsOk) continue;

                    destFolder = fldr;
                    break;
                }

                if (destFolder == null)
                {
                    logger.Error("Could not find the file to move: {0}", this.FullServerPath);
                    return true;
                }

                // keep the original drop folder for later (take a copy, not a reference)
                SVR_ImportFolder dropFolder = this.ImportFolder;

                // we can only move the file if it has an anime associated with it
                List<CrossRef_File_Episode> xrefs = this.VideoLocal.EpisodeCrossRefs;
                if (xrefs.Count == 0) return true;
                CrossRef_File_Episode xref = xrefs[0];

                // find the series associated with this episode
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                if (series == null) return true;

                // find where the other files are stored for this series
                // if there are no other files except for this one, it means we need to create a new location
                bool foundLocation = false;
                string newFullPath = "";

                // sort the episodes by air date, so that we will move the file to the location of the latest episode
                List<SVR_AnimeEpisode> allEps = series.GetAnimeEpisodes()
                    .OrderByDescending(a => a.AniDB_Episode.AirDate)
                    .ToList();

                IDirectory destination = null;

                foreach (SVR_AnimeEpisode ep in allEps)
                {
                    // check if this episode belongs to more than one anime
                    // if it does we will ignore it
                    List<CrossRef_File_Episode> fileEpXrefs =
                        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(ep.AniDB_EpisodeID);
                    int? animeID = null;
                    bool crossOver = false;
                    foreach (CrossRef_File_Episode fileEpXref in fileEpXrefs)
                    {
                        if (!animeID.HasValue)
                            animeID = fileEpXref.AnimeID;
                        else
                        {
                            if (animeID.Value != fileEpXref.AnimeID)
                                crossOver = true;
                        }
                    }
                    if (crossOver) continue;

                    foreach (SVR_VideoLocal vid in ep.GetVideoLocals()
                        .Where(a => a.Places.Any(b => b.ImportFolder.CloudID == destFolder.CloudID &&
                                                      b.ImportFolder.IsDropSource == 0)).ToList())
                    {
                        if (vid.VideoLocalID == this.VideoLocalID) continue;

                        SVR_VideoLocal_Place place =
                            vid.Places.FirstOrDefault(a => a.ImportFolder.CloudID == destFolder.CloudID);
                        string thisFileName = place?.FullServerPath;
                        if (thisFileName == null) continue;
                        string folderName = Path.GetDirectoryName(thisFileName);

                        FileSystemResult<IObject> dir = f.Resolve(folderName);
                        if (!dir.IsOk) continue;
                        // ensure we aren't moving to the current directory
                        if (folderName.Equals(Path.GetDirectoryName(FullServerPath),
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                        destination = dir.Result as IDirectory;
                        // Not a directory
                        if (destination == null) continue;
                        newFullPath = folderName;
                        foundLocation = true;
                        break;
                    }
                    if (foundLocation) break;
                }

                if (!foundLocation)
                {
                    // we need to create a new folder
                    string newFolderName = Utils.RemoveInvalidFolderNameCharacters(series.GetSeriesName());

                    newFullPath = Path.Combine(destFolder.ImportFolderLocation, newFolderName);
                    FileSystemResult<IObject> dirn = f.Resolve(newFullPath);
                    if (!dirn.IsOk)
                    {
                        dirn = f.Resolve(destFolder.ImportFolderLocation);
                        if (dirn.IsOk)
                        {
                            IDirectory d = (IDirectory) dirn.Result;
                            FileSystemResult<IDirectory> d2 = Task
                                .Run(async () => await d.CreateDirectoryAsync(newFolderName, null))
                                .Result;
                            destination = d2.Result;
                        }
                        else
                        {
                            logger.Error("Import folder couldn't be resolved: {0}", destFolder.ImportFolderLocation);
                            newFullPath = null;
                        }
                    }
                    else if (dirn.Result is IFile)
                    {
                        logger.Error("Destination folder is a file: {0}", newFolderName);
                        newFullPath = null;
                    }
                    else
                    {
                        destination = (IDirectory) dirn.Result;
                    }
                }

                if (string.IsNullOrEmpty(newFullPath))
                {
                    return true;
                }

                // We've already resolved FullServerPath, so it doesn't need to be checked
                string newFullServerPath = Path.Combine(newFullPath, Path.GetFileName(this.FullServerPath));
                Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullServerPath);
                if (tup == null)
                {
                    logger.Error($"Unable to LOCATE file {newFullServerPath} inside the import folders");
                    return true;
                }

                // Last ditch effort to ensure we aren't moving a file unto itself
                if (newFullServerPath.Equals(FullServerPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Error($"Resolved to move {newFullServerPath} unto itself. NOT MOVING");
                    return true;
                }

                logger.Info("Moving file from {0} to {1}", this.FullServerPath, newFullServerPath);

                FileSystemResult<IObject> dst = f.Resolve(newFullServerPath);
                if (dst.IsOk)
                {
                    logger.Trace(
                        "Not moving file as it already exists at the new location, deleting source file instead: {0} --- {1}",
                        this.FullServerPath, newFullServerPath);

                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    FileSystemResult fr = new FileSystemResult();
                    try
                    {
                        fr = source_file.Delete(false);
                        if (!fr.IsOk)
                        {
                            logger.Warn("Unable to DELETE file: {0} error {1}", this.FullServerPath,
                                fr?.Error ?? String.Empty);
                        }
                        this.ImportFolderID = tup.Item1.ImportFolderID;
                        this.FilePath = tup.Item2;
                        RepoFactory.VideoLocalPlace.Save(this);

                        // check for any empty folders in drop folder
                        // only for the drop folder
                        if (dropFolder.IsDropSource == 1)
                        {
                            FileSystemResult<IObject> dd = f.Resolve(dropFolder.ImportFolderLocation);
                            if (dd != null && dd.IsOk && dd.Result is IDirectory)
                            {
                                RecursiveDeleteEmptyDirectories((IDirectory) dd.Result, true);
                            }
                        }
                    }
                    catch
                    {
                        logger.Error("Unable to DELETE file: {0} error {1}", this.FullServerPath,
                            fr?.Error ?? String.Empty);
                    }
                }
                else
                {
                    FileSystemResult fr = source_file.Move(destination);
                    if (!fr.IsOk)
                    {
                        logger.Error("Unable to MOVE file: {0} to {1} error {2}", this.FullServerPath,
                            newFullServerPath, fr?.Error ?? "No Error String");
                        return false;
                    }
                    string originalFileName = this.FullServerPath;


                    this.ImportFolderID = tup.Item1.ImportFolderID;
                    this.FilePath = tup.Item2;
                    RepoFactory.VideoLocalPlace.Save(this);

                    try
                    {
                        // move any subtitle files
                        foreach (string subtitleFile in Utils.GetPossibleSubtitleFiles(originalFileName))
                        {
                            FileSystemResult<IObject> src = f.Resolve(subtitleFile);
                            if (src.IsOk && src.Result is IFile)
                            {
                                string newSubPath = Path.Combine(Path.GetDirectoryName(newFullServerPath),
                                    ((IFile) src.Result).Name);
                                dst = f.Resolve(newSubPath);
                                if (dst.IsOk && dst.Result is IFile)
                                {
                                    FileSystemResult fr2 = src.Result.Delete(false);
                                    if (!fr2.IsOk)
                                    {
                                        logger.Warn("Unable to DELETE file: {0} error {1}", subtitleFile,
                                            fr2?.Error ?? String.Empty);
                                    }
                                }
                                else
                                {
                                    FileSystemResult fr2 = ((IFile) src.Result).Move(destination);
                                    if (!fr2.IsOk)
                                    {
                                        logger.Error("Unable to MOVE file: {0} to {1} error {2)", subtitleFile,
                                            newSubPath, fr2?.Error ?? String.Empty);
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
                        FileSystemResult<IObject> dd = f.Resolve(dropFolder.ImportFolderLocation);
                        if (dd != null && dd.IsOk && dd.Result is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory) dd.Result, true);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Could not MOVE file: {this.FullServerPath} -- {ex.ToString()}";
                logger.Error(ex, msg);
            }
            return true;
        }

        private void RecursiveDeleteEmptyDirectories(IDirectory dir, bool importfolder)
        {
            FileSystemResult fr = dir.Populate();
            if (fr.IsOk)
            {
                if (dir.Files.Count > 0 && dir.Directories.Count == 0)
                    return;
                foreach (IDirectory d in dir.Directories)
                    RecursiveDeleteEmptyDirectories(d, false);
            }
            if (importfolder)
                return;
            fr = dir.Populate();
            if (fr.IsOk)
            {
                if (dir.Files.Count == 0 && dir.Directories.Count == 0)
                {
                    fr = dir.Delete(true);
                    if (!fr.IsOk)
                    {
                        logger.Warn("Unable to DELETE directory: {0} error {1}", dir.FullName,
                            fr?.Error ?? String.Empty);
                    }
                }
            }
        }
    }
}