using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Commands;
using JMMServer.Databases;
using JMMServer.FileHelper;
using JMMServer.FileHelper.MediaInfo;
using JMMServer.FileHelper.Subtitles;
using JMMServer.PlexAndKodi;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using Nancy.Extensions;
using NHibernate;
using NLog;
using NutzCode.CloudFileSystem;
using Media = JMMContracts.PlexAndKodi.Media;
using Stream = System.IO.Stream;

namespace JMMServer.Entities
{
    public class VideoLocal_Place
    {
        public int VideoLocal_Place_ID { get; private set; }
        public int VideoLocalID { get; set; }
        public string FilePath { get; set; }
        public int ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }

        public ImportFolder ImportFolder => RepoFactory.ImportFolder.GetByID(ImportFolderID);

	    public string FullServerPath
	    {
		    get
		    {
			    if (string.IsNullOrEmpty(ImportFolder?.ParsedImportFolderLocation) || string.IsNullOrEmpty(FilePath)) return null;
			    return Path.Combine(ImportFolder.ParsedImportFolderLocation, FilePath);
		    }
	    }

        public VideoLocal VideoLocal => RepoFactory.VideoLocal.GetByID(VideoLocalID);

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void RenameFile(string renameScript)
        {
            string renamed = RenameFileHelper.GetNewFileName(VideoLocal, renameScript);
            if (string.IsNullOrEmpty(renamed)) return;

            IFileSystem filesys = ImportFolder.FileSystem;
            if (filesys == null)
                return;
            // actually rename the file
            string fullFileName = this.FullServerPath;

            // check if the file exists

            FileSystemResult<IObject> re = filesys.Resolve(fullFileName);
            if ((re == null) || (!re.IsOk))
            {
                logger.Error("Error could not find the original file for renaming: " + fullFileName);
                return;
            }
            IObject file = re.Result;
            // actually rename the file
            string path = Path.GetDirectoryName(fullFileName);
            string newFullName = (path == null ? null: Path.Combine(path, renamed));

            try
            {
                logger.Info($"Renaming file From ({fullFileName}) to ({newFullName})....");

                if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Info($"Renaming file SKIPPED! no change From ({fullFileName}) to ({newFullName})");
	                return;
                }

				FileSystemResult r = file?.FileSystem?.Resolve(newFullName);
				if (r != null && r.IsOk)
				{
					logger.Info($"Renaming file SKIPPED! Destination Exists ({newFullName})");
					return;
				}

				r = file.Rename(renamed);
	            if (r == null || !r.IsOk)
	            {
		            logger.Info($"Renaming file FAILED! From ({fullFileName}) to ({newFullName}) - {r?.Error ?? "Result is null"}");
		            return;
	            }

				logger.Info($"Renaming file SUCCESS! From ({fullFileName}) to ({newFullName})");
				Tuple<ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
				if (tup == null)
				{
					logger.Error($"Unable to LOCATE file {newFullName} inside the import folders");
					return;
				}
				this.FilePath = tup.Item2;
				RepoFactory.VideoLocalPlace.Save(this);

            }
            catch (Exception ex)
            {
                logger.Info($"Renaming file FAILED! From ({fullFileName}) to ({newFullName}) - {ex.Message}");
                logger.Error( ex,ex.ToString());
            }
        }

	    public void RemoveRecord()
	    {
		    logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", FullServerPath);
		    List<AnimeEpisode> episodesToUpdate = new List<AnimeEpisode>();
		    List<AnimeSeries> seriesToUpdate = new List<AnimeSeries>();
		    VideoLocal v = VideoLocal;
		    using (var session = DatabaseFactory.SessionFactory.OpenSession())
		    {
			    if (v.Places.Count <= 1)
			    {
				    episodesToUpdate.AddRange(v.GetAnimeEpisodes());
				    seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));
				    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
				    RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
				    CommandRequest_DeleteFileFromMyList cmdDel =
					    new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
				    cmdDel.Save();
			    }
			    else
			    {
				    episodesToUpdate.AddRange(v.GetAnimeEpisodes());
				    seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));
				    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
			    }
			    episodesToUpdate = episodesToUpdate.DistinctBy(a => a.AnimeEpisodeID).ToList();
			    foreach (AnimeEpisode ep in episodesToUpdate)
			    {
				    if (ep.AnimeEpisodeID == 0)
				    {
					    ep.PlexContract = null;
					    RepoFactory.AnimeEpisode.SaveWithOpenTransaction(session, ep);
				    }
				    try
				    {
					    ep.PlexContract = Helper.GenerateVideoFromAnimeEpisode(ep);
					    RepoFactory.AnimeEpisode.SaveWithOpenTransaction(session, ep);
				    }
				    catch (Exception ex)
				    {
					    LogManager.GetCurrentClassLogger().Error(ex, ex.ToString());
				    }
			    }
			    seriesToUpdate = seriesToUpdate.DistinctBy(a => a.AnimeSeriesID).ToList();
			    foreach (AnimeSeries ser in seriesToUpdate)
			    {
				    ser.QueueUpdateStats();
			    }
		    }
	    }

	    public void RemoveRecordWithOpenTransaction(ISession session, List<AnimeEpisode> episodesToUpdate,
		    List<AnimeSeries> seriesToUpdate)
	    {
		    logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", FullServerPath);
		    VideoLocal v = VideoLocal;
		    if (v.Places.Count <= 1)
		    {
			    episodesToUpdate.AddRange(v.GetAnimeEpisodes());
			    seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));
			    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
			    RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
			    CommandRequest_DeleteFileFromMyList cmdDel =
				    new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
			    cmdDel.Save();
		    }
		    else
		    {
			    episodesToUpdate.AddRange(v.GetAnimeEpisodes());
			    seriesToUpdate.AddRange(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()));
			    RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, this);
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

        public static void FillVideoInfoFromMedia(VideoLocal info, Media m)
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
                        (long)double.Parse(m.Duration, NumberStyles.Any, CultureInfo.InvariantCulture);
                else
                    info.Duration = 0;
            }
            else
                info.Duration = 0;

            info.VideoBitrate = info.VideoBitDepth = info.VideoFrameRate = info.AudioBitrate = string.Empty;
            List<JMMContracts.PlexAndKodi.Stream> vparts = m.Parts[0].Streams.Where(a => a.StreamType == "1").ToList();
            if (vparts.Count > 0)
            {
                if (!string.IsNullOrEmpty(vparts[0].Bitrate))
                    info.VideoBitrate = vparts[0].Bitrate;
                if (!string.IsNullOrEmpty(vparts[0].BitDepth))
                    info.VideoBitDepth = vparts[0].BitDepth;
                if (!string.IsNullOrEmpty(vparts[0].FrameRate))
                    info.VideoFrameRate = vparts[0].FrameRate;
            }
            List<JMMContracts.PlexAndKodi.Stream> aparts = m.Parts[0].Streams.Where(a => a.StreamType == "2").ToList();
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
                Media m=null;
                List<Providers.Azure.Media> webmedias = AzureWebAPI.Get_Media(VideoLocal.ED2KHash);
                if (webmedias != null && webmedias.Count > 0)
                {
                    m = webmedias[0].GetMedia();
                }
                if (m == null)
                {

                    string name = (ImportFolder.CloudID == null)
                        ? FullServerPath.Replace("/", "\\")
                        : ((IProvider)null).ReplaceSchemeHost(((IProvider)null).ConstructVideoLocalStream(0,
                            VideoLocalID.ToString(), "file", false));
                    m = MediaConvert.Convert(name, GetFile()); //Mediainfo should have libcurl.dll for http
                    if (string.IsNullOrEmpty(m.Duration))
                        m = null;
                    if (m != null)
                        AzureWebAPI.Send_Media(VideoLocal.ED2KHash, m);
                }


                if (m != null)
                {
                    VideoLocal info = VideoLocal;
                    FillVideoInfoFromMedia(info, m);

                    m.Id = VideoLocalID.ToString();
                    List<JMMContracts.PlexAndKodi.Stream> subs = SubtitleHelper.GetSubtitleStreams(this);
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
                        foreach (JMMContracts.PlexAndKodi.Stream ss in p.Streams.ToArray())
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
        public void RenameIfRequired()
        {
            try
            {
                RenameScript defaultScript = RepoFactory.RenameScript.GetDefaultScript();

                if (defaultScript == null) return;

                RenameFile(defaultScript.Script);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return;
            }
        }

        public void MoveFileIfRequired()
        {
            try
            {
                logger.Trace("Attempting to MOVE file: {0}", this.FullServerPath);

                // check if this file is in the drop folder
                // otherwise we don't need to move it
                if (ImportFolder.IsDropSource == 0)
                {
                    logger.Trace("Not moving file as it is NOT in the drop folder: {0}", this.FullServerPath);
                    return;
                }
                IFileSystem f = this.ImportFolder.FileSystem;
                if (f == null)
                {
                    logger.Trace("Unable to MOVE, filesystem not working: {0}", this.FullServerPath);
                    return;

                }

                FileSystemResult<IObject> fsrresult = f.Resolve(FullServerPath);
                if (!fsrresult.IsOk)
                {
                    logger.Error("Could not find the file to move: {0}", this.FullServerPath);
                    return;
                }
                IFile source_file = fsrresult.Result as IFile;
                if (source_file == null)
                {
                    logger.Error("Could not find the file to move: {0}", this.FullServerPath);
                    return;
                }
                // find the default destination
                ImportFolder destFolder = null;
                foreach (ImportFolder fldr in RepoFactory.ImportFolder.GetAll().Where(a => a.CloudID == ImportFolder.CloudID))
                {
                    if (fldr.IsDropDestination == 1)
                    {
                        destFolder = fldr;
                        break;
                    }
                }

                if (destFolder == null) return;

                FileSystemResult<IObject> re = f.Resolve(destFolder.ImportFolderLocation);
                if (!re.IsOk)
                    return;

                // keep the original drop folder for later (take a copy, not a reference)
                ImportFolder dropFolder = this.ImportFolder;

                // we can only move the file if it has an anime associated with it
                List<CrossRef_File_Episode> xrefs = this.VideoLocal.EpisodeCrossRefs;
                if (xrefs.Count == 0) return;
                CrossRef_File_Episode xref = xrefs[0];

                // find the series associated with this episode
                AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                if (series == null) return;

                // find where the other files are stored for this series
                // if there are no other files except for this one, it means we need to create a new location
                bool foundLocation = false;
                string newFullPath = "";

                // sort the episodes by air date, so that we will move the file to the location of the latest episode
                List<AnimeEpisode> allEps = series.GetAnimeEpisodes().OrderByDescending(a => a.AniDB_EpisodeID).ToList();

                IDirectory destination = null;

                foreach (AnimeEpisode ep in allEps)
                {
                    // check if this episode belongs to more than one anime
                    // if it does we will ignore it
                    List<CrossRef_File_Episode> fileEpXrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(ep.AniDB_EpisodeID);
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

                    foreach (VideoLocal vid in ep.GetVideoLocals().Where(a => a.Places.Any(b=>b.ImportFolder.CloudID == destFolder.CloudID && b.ImportFolder.IsDropSource==0)))
                    {
                        if (vid.VideoLocalID != this.VideoLocalID)
                        {
                            VideoLocal_Place place=vid.Places.FirstOrDefault(a=>a.ImportFolder.CloudID==destFolder.CloudID);
                            string thisFileName = place?.FullServerPath;
                            string folderName = Path.GetDirectoryName(thisFileName);

                            FileSystemResult<IObject> dir = f.Resolve(folderName);
                            if (dir.IsOk)
                            {
                                destination = (IDirectory)dir.Result;
                                newFullPath = folderName;
                                foundLocation = true;
                                break;
                            }
                        }
                    }
                    if (foundLocation) break;
                }

                if (!foundLocation)
                {
                    // we need to create a new folder
                    string newFolderName = Utils.RemoveInvalidFolderNameCharacters(series.GetAnime().PreferredTitle);

                    newFullPath =Path.Combine(destFolder.ParsedImportFolderLocation, newFolderName);
                    FileSystemResult<IObject> dirn = f.Resolve(newFullPath);
                    if (!dirn.IsOk)
                    {
                        dirn = f.Resolve(destFolder.ImportFolderLocation);
                        if (dirn.IsOk)
                        {
                            IDirectory d = (IDirectory)dirn.Result;
                            FileSystemResult<IDirectory> d2 = Task.Run(async () => await d.CreateDirectoryAsync(newFolderName, null)).Result;
                            destination = d2.Result;

                        }
                    }
                    else if (dirn.Result is IFile)
                    {
                        logger.Error("Destination folder is a file: {0}", newFolderName);
                    }
                    else
                    {
                        destination = (IDirectory) dirn.Result;
                    }
                }
                
                string newFullServerPath = Path.Combine(newFullPath, Path.GetFileName(this.FullServerPath));
                Tuple<ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullServerPath);
                if (tup == null)
                {
                    logger.Error($"Unable to LOCATE file {newFullServerPath} inside the import folders");
                    return;
                }

                logger.Info("Moving file from {0} to {1}", this.FullServerPath, newFullServerPath);

                FileSystemResult<IObject> dst = f.Resolve(newFullServerPath);
                if (dst.IsOk)
                {
                    logger.Trace("Not moving file as it already exists at the new location, deleting source file instead: {0} --- {1}",
                        this.FullServerPath, newFullServerPath);

                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    FileSystemResult fr = new FileSystemResult();
                    try
                    {
                        fr = source_file.Delete(false);
                        if (!fr.IsOk)
                        {
                            logger.Warn("Unable to DELETE file: {0} error {1}", this.FullServerPath, fr?.Error ?? String.Empty);
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
                                RecursiveDeleteEmptyDirectories((IDirectory)dd.Result, true);
                            }
                        }
                    }
                    catch
                    {
                        logger.Error("Unable to DELETE file: {0} error {1}", this.FullServerPath, fr?.Error ?? String.Empty);
                    }
                }
                else
                {
                    FileSystemResult fr = source_file.Move(destination);
                    if (!fr.IsOk)
                    {
                        logger.Error("Unable to MOVE file: {0} to {1} error {2)", this.FullServerPath, newFullServerPath, fr?.Error ?? String.Empty);
                        return;
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
                                string newSubPath = Path.Combine(Path.GetDirectoryName(newFullServerPath), ((IFile)src.Result).Name);
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
                                    FileSystemResult fr2 = ((IFile)src.Result).Move(destination);
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
                        logger.Error( ex,ex.ToString());
                    }

                    // check for any empty folders in drop folder
                    // only for the drop folder
                    if (dropFolder.IsDropSource == 1)
                    {
                        FileSystemResult<IObject> dd = f.Resolve(dropFolder.ImportFolderLocation);
                        if (dd != null && dd.IsOk && dd.Result is IDirectory)
                            RecursiveDeleteEmptyDirectories((IDirectory)dd.Result,true);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Could not MOVE file: {this.FullServerPath} -- {ex.ToString()}";
                logger.Error( ex,msg);
            }
        }

        public Contract_VideoLocal_Place ToContract()
        {
            Contract_VideoLocal_Place v = new Contract_VideoLocal_Place
            {
                FilePath = FilePath,
                ImportFolderID = ImportFolderID,
                ImportFolderType = ImportFolderType,
                VideoLocalID = VideoLocalID,
                ImportFolder = ImportFolder.ToContract(),
                VideoLocal_Place_ID = VideoLocal_Place_ID
            };
            return v;
        }
        private void RecursiveDeleteEmptyDirectories(IDirectory dir, bool importfolder)
        {
            FileSystemResult fr = dir.Populate();
            if (fr.IsOk)
            {
                if (dir.Files.Count > 0 && dir.Directories.Count == 0)
                    return;
                foreach (IDirectory d in dir.Directories)
                    RecursiveDeleteEmptyDirectories(d,false);
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
                        logger.Warn("Unable to DELETE directory: {0} error {1}", dir.FullName, fr?.Error ?? String.Empty);
                    }
                }
            }
        }

    }
}
