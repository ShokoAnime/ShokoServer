using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMServer.Commands;
using JMMServer.Commands.MAL;
using JMMServer.Repositories;
using NHibernate;
using NLog;

namespace JMMServer.Entities
{
    public class VideoLocal : IHash
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public int VideoLocalID { get; private set; }
        public string FilePath { get; set; }
        public int ImportFolderID { get; set; }
        public string Hash { get; set; }
        public string CRC32 { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public int HashSource { get; set; }
        public int IsIgnored { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public int IsVariation { get; set; }

        public ImportFolder ImportFolder
        {
            get
            {
                var repNS = new ImportFolderRepository();
                return repNS.GetByID(ImportFolderID);
            }
        }

        public string FullServerPath
        {
            get { return Path.Combine(ImportFolder.ImportFolderLocation, FilePath); }
        }

        public VideoInfo VideoInfo
        {
            get
            {
                var repVI = new VideoInfoRepository();
                return repVI.GetByHash(Hash);
            }
        }

        public AniDB_ReleaseGroup ReleaseGroup
        {
            get
            {
                var anifile = GetAniDBFile();
                if (anifile == null) return null;

                var repRG = new AniDB_ReleaseGroupRepository();
                return repRG.GetByGroupID(anifile.GroupID);
            }
        }

        public List<CrossRef_File_Episode> EpisodeCrossRefs
        {
            get
            {
                if (Hash.Length == 0) return new List<CrossRef_File_Episode>();

                var rep = new CrossRef_File_EpisodeRepository();
                return rep.GetByHash(Hash);
            }
        }

        public long FileSize { get; set; }

        public string ED2KHash
        {
            get { return Hash; }
            set { Hash = value; }
        }

        public string Info
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath))
                    return "";
                return FilePath;
            }
        }

        public string ToStringDetailed()
        {
            var sb = new StringBuilder("");
            sb.Append(Environment.NewLine);
            sb.Append("VideoLocalID: " + VideoLocalID);
            sb.Append(Environment.NewLine);
            sb.Append("FilePath: " + FilePath);
            sb.Append(Environment.NewLine);
            sb.Append("ImportFolderID: " + ImportFolderID);
            sb.Append(Environment.NewLine);
            sb.Append("Hash: " + Hash);
            sb.Append(Environment.NewLine);
            sb.Append("FileSize: " + FileSize);
            sb.Append(Environment.NewLine);

            try
            {
                if (ImportFolder != null)
                    sb.Append("ImportFolderLocation: " + ImportFolder.ImportFolderLocation);
            }
            catch (Exception ex)
            {
                sb.Append("ImportFolderLocation: " + ex);
            }

            sb.Append(Environment.NewLine);

            return sb.ToString();
        }

        public AniDB_File GetAniDBFile()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAniDBFile(session);
            }
        }

        public AniDB_File GetAniDBFile(ISession session)
        {
            var repAniFile = new AniDB_FileRepository();
            return repAniFile.GetByHash(session, Hash);
        }

        public VideoLocal_User GetUserRecord(int userID)
        {
            var repVidUser = new VideoLocal_UserRepository();
            return repVidUser.GetByUserIDAndVideoLocalID(userID, VideoLocalID);
        }

        public List<AnimeEpisode> GetAnimeEpisodes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAnimeEpisodes(session);
            }
        }

        public List<AnimeEpisode> GetAnimeEpisodes(ISession session)
        {
            var repEps = new AnimeEpisodeRepository();
            return repEps.GetByHash(session, Hash);
        }

        private void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            var repVidUsers = new VideoLocal_UserRepository();
            var vidUserRecord = GetUserRecord(userID);
            if (watched)
            {
                if (vidUserRecord == null)
                {
                    vidUserRecord = new VideoLocal_User();
                    vidUserRecord.WatchedDate = DateTime.Now;
                }
                vidUserRecord.JMMUserID = userID;
                vidUserRecord.VideoLocalID = VideoLocalID;

                if (watchedDate.HasValue)
                {
                    if (updateWatchedDate)
                        vidUserRecord.WatchedDate = watchedDate.Value;
                }

                repVidUsers.Save(vidUserRecord);
            }
            else
            {
                if (vidUserRecord != null)
                    repVidUsers.Delete(vidUserRecord.VideoLocal_UserID);
            }
        }

        public void ToggleWatchedStatus(bool watched, int userID)
        {
            ToggleWatchedStatus(watched, true, null, true, true, userID, true, true);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
            bool updateStatsCache, int userID,
            bool syncTrakt, bool updateWatchedDate)
        {
            var repVids = new VideoLocalRepository();
            var repEpisodes = new AnimeEpisodeRepository();
            var repAniFile = new AniDB_FileRepository();
            var repCross = new CrossRef_File_EpisodeRepository();
            var repVidUsers = new VideoLocal_UserRepository();
            var repUsers = new JMMUserRepository();
            var repEpisodeUsers = new AnimeEpisode_UserRepository();

            var user = repUsers.GetByID(userID);
            if (user == null) return;

            var aniDBUsers = repUsers.GetAniDBUsers();

            // update the video file to watched
            var mywatched = watched ? 1 : 0;

            if (user.IsAniDBUser == 0)
                SaveWatchedStatus(watched, userID, watchedDate, updateWatchedDate);
            else
            {
                // if the user is AniDB user we also want to update any other AniDB
                // users to keep them in sync
                foreach (var juser in aniDBUsers)
                {
                    if (juser.IsAniDBUser == 1)
                        SaveWatchedStatus(watched, juser.JMMUserID, watchedDate, updateWatchedDate);
                }
            }


            // now lets find all the associated AniDB_File record if there is one
            if (user.IsAniDBUser == 1)
            {
                var aniFile = repAniFile.GetByHash(Hash);
                if (aniFile != null)
                {
                    aniFile.IsWatched = mywatched;

                    if (watched)
                    {
                        if (watchedDate.HasValue)
                            aniFile.WatchedDate = watchedDate;
                        else
                            aniFile.WatchedDate = DateTime.Now;
                    }
                    else
                        aniFile.WatchedDate = null;


                    repAniFile.Save(aniFile, false);
                }

                if (updateOnline)
                {
                    if ((watched && ServerSettings.AniDB_MyList_SetWatched) ||
                        (!watched && ServerSettings.AniDB_MyList_SetUnwatched))
                    {
                        var cmd = new CommandRequest_UpdateMyListFileStatus(Hash, watched, false,
                            watchedDate.HasValue ? Utils.GetAniDBDateAsSeconds(watchedDate) : 0);
                        cmd.Save();
                    }
                }
            }

            // now find all the episode records associated with this video file
            // but we also need to check if theer are any other files attached to this episode with a watched
            // status, 


            AnimeSeries ser = null;
            // get all files associated with this episode
            var xrefs = repCross.GetByHash(Hash);
            if (watched)
            {
                // find the total watched percentage
                // eg one file can have a % = 100
                // or if 2 files make up one episodes they will each have a % = 50

                foreach (var xref in xrefs)
                {
                    // get the episode for this file
                    var ep = repEpisodes.GetByAniDBEpisodeID(xref.EpisodeID);
                    if (ep == null) continue;

                    // get all the files for this episode
                    var epPercentWatched = 0;
                    foreach (var filexref in ep.FileCrossRefs)
                    {
                        var vidUser = filexref.GetVideoLocalUserRecord(userID);
                        if (vidUser != null)
                        {
                            // if not null means it is watched
                            epPercentWatched += filexref.Percentage;
                        }

                        if (epPercentWatched > 95) break;
                    }

                    if (epPercentWatched > 95)
                    {
                        ser = ep.GetAnimeSeries();

                        if (user.IsAniDBUser == 0)
                            ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
                        else
                        {
                            // if the user is AniDB user we also want to update any other AniDB
                            // users to keep them in sync
                            foreach (var juser in aniDBUsers)
                            {
                                if (juser.IsAniDBUser == 1)
                                    ep.SaveWatchedStatus(true, juser.JMMUserID, watchedDate, updateWatchedDate);
                            }
                        }

                        if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                            !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                        {
                            var cmdSyncTrakt =
                                new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Add);
                            cmdSyncTrakt.Save();
                        }

                        if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                            !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                        {
                            var cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                            cmdMAL.Save();
                        }
                    }
                }
            }
            else
            {
                // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
                foreach (var xrefEp in xrefs)
                {
                    var ep = repEpisodes.GetByAniDBEpisodeID(xrefEp.EpisodeID);
                    if (ep == null) continue;
                    ser = ep.GetAnimeSeries();

                    // get all the files for this episode
                    var epPercentWatched = 0;
                    foreach (var filexref in ep.FileCrossRefs)
                    {
                        var vidUser = filexref.GetVideoLocalUserRecord(userID);
                        if (vidUser != null)
                            epPercentWatched += filexref.Percentage;

                        if (epPercentWatched > 95) break;
                    }

                    if (epPercentWatched < 95)
                    {
                        if (user.IsAniDBUser == 0)
                            ep.SaveWatchedStatus(false, userID, watchedDate, true);
                        else
                        {
                            // if the user is AniDB user we also want to update any other AniDB
                            // users to keep them in sync
                            foreach (var juser in aniDBUsers)
                            {
                                if (juser.IsAniDBUser == 1)
                                    ep.SaveWatchedStatus(false, juser.JMMUserID, watchedDate, true);
                            }
                        }

                        if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                            !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                        {
                            var cmdSyncTrakt = new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID,
                                TraktSyncAction.Remove);
                            cmdSyncTrakt.Save();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                    !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    var cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                    cmdMAL.Save();
                }
            }


            // update stats for groups and series
            if (ser != null && updateStats)
            {
                // update all the groups above this series in the heirarchy
                ser.UpdateStats(true, true, true);
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
            }

            //if (ser != null && updateStatsCache)
            //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
        }

        public override string ToString()
        {
            return string.Format("{0} --- {1}", FullServerPath, Hash);
        }

        public void RenameFile(string renameScript)
        {
            var renamed = RenameFileHelper.GetNewFileName(this, renameScript);
            if (string.IsNullOrEmpty(renamed)) return;

            var repFolders = new ImportFolderRepository();
            var repVids = new VideoLocalRepository();

            // actually rename the file
            var fullFileName = FullServerPath;

            // check if the file exists
            if (!File.Exists(fullFileName))
            {
                logger.Error("Error could not find the original file for renaming: " + fullFileName);
                return;
            }

            // actually rename the file
            var path = Path.GetDirectoryName(fullFileName);
            var newFullName = Path.Combine(path, renamed);

            try
            {
                logger.Info(string.Format("Renaming file From ({0}) to ({1})....", fullFileName, newFullName));

                if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Info(string.Format("Renaming file SKIPPED, no change From ({0}) to ({1})", fullFileName,
                        newFullName));
                }
                else
                {
                    File.Move(fullFileName, newFullName);
                    logger.Info(string.Format("Renaming file SUCCESS From ({0}) to ({1})", fullFileName, newFullName));

                    var newPartialPath = "";
                    var folderID = ImportFolderID;

                    DataAccessHelper.GetShareAndPath(newFullName, repFolders.GetAll(), ref folderID, ref newPartialPath);

                    FilePath = newPartialPath;
                    repVids.Save(this);
                }
            }
            catch (Exception ex)
            {
                logger.Info(string.Format("Renaming file FAIL From ({0}) to ({1}) - {2}", fullFileName, newFullName,
                    ex.Message));
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void RenameIfRequired()
        {
            try
            {
                var repScripts = new RenameScriptRepository();
                var defaultScript = repScripts.GetDefaultScript();

                if (defaultScript == null) return;

                RenameFile(defaultScript.Script);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void MoveFileIfRequired()
        {
            try
            {
                logger.Trace("Attempting to move file: {0}", FullServerPath);

                // check if this file is in the drop folder
                // otherwise we don't need to move it
                if (ImportFolder.IsDropSource == 0)
                {
                    logger.Trace("Not moving file as it is NOT in the drop folder: {0}", FullServerPath);
                    return;
                }

                if (!File.Exists(FullServerPath))
                {
                    logger.Error("Could not find the file to move: {0}", FullServerPath);
                    return;
                }

                // find the default destination
                ImportFolder destFolder = null;
                var repFolders = new ImportFolderRepository();
                foreach (var fldr in repFolders.GetAll())
                {
                    if (fldr.IsDropDestination == 1)
                    {
                        destFolder = fldr;
                        break;
                    }
                }

                if (destFolder == null) return;

                if (!Directory.Exists(destFolder.ImportFolderLocation)) return;

                // keep the original drop folder for later (take a copy, not a reference)
                var dropFolder = ImportFolder;

                // we can only move the file if it has an anime associated with it
                var xrefs = EpisodeCrossRefs;
                if (xrefs.Count == 0) return;
                var xref = xrefs[0];

                // find the series associated with this episode
                var repSeries = new AnimeSeriesRepository();
                var series = repSeries.GetByAnimeID(xref.AnimeID);
                if (series == null) return;

                // find where the other files are stored for this series
                // if there are no other files except for this one, it means we need to create a new location
                var foundLocation = false;
                var newFullPath = "";

                // sort the episodes by air date, so that we will move the file to the location of the latest episode
                var allEps = series.GetAnimeEpisodes();
                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("AniDB_EpisodeID", true, SortType.eInteger));
                allEps = Sorting.MultiSort(allEps, sortCriteria);

                var repAnime = new AniDB_AnimeRepository();
                var repFileEpXref = new CrossRef_File_EpisodeRepository();

                foreach (var ep in allEps)
                {
                    // check if this episode belongs to more than one anime
                    // if it does we will ignore it
                    var fileEpXrefs = repFileEpXref.GetByEpisodeID(ep.AniDB_EpisodeID);
                    int? animeID = null;
                    var crossOver = false;
                    foreach (var fileEpXref in fileEpXrefs)
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

                    foreach (var vid in ep.GetVideoLocals())
                    {
                        if (vid.VideoLocalID != VideoLocalID)
                        {
                            // make sure this folder is not the drop source
                            if (vid.ImportFolder.IsDropSource == 1) continue;

                            var thisFileName = vid.FullServerPath;
                            var folderName = Path.GetDirectoryName(thisFileName);

                            if (Directory.Exists(folderName))
                            {
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
                    var newFolderName = Utils.RemoveInvalidFolderNameCharacters(series.GetAnime().MainTitle);
                    newFullPath = Path.Combine(destFolder.ImportFolderLocation, newFolderName);
                    if (!Directory.Exists(newFullPath))
                        Directory.CreateDirectory(newFullPath);
                }

                var newFolderID = 0;
                var newPartialPath = "";
                var newFullServerPath = Path.Combine(newFullPath, Path.GetFileName(FullServerPath));

                DataAccessHelper.GetShareAndPath(newFullServerPath, repFolders.GetAll(), ref newFolderID,
                    ref newPartialPath);
                logger.Info("Moving file from {0} to {1}", FullServerPath, newFullServerPath);

                if (File.Exists(newFullServerPath))
                {
                    logger.Trace(
                        "Not moving file as it already exists at the new location, deleting source file instead: {0} --- {1}",
                        FullServerPath, newFullServerPath);

                    // if the file already exists, we can just delete the source file instead
                    // this is safer than deleting and moving
                    File.Delete(FullServerPath);

                    ImportFolderID = newFolderID;
                    FilePath = newPartialPath;
                    var repVids = new VideoLocalRepository();
                    repVids.Save(this);
                }
                else
                {
                    var originalFileName = FullServerPath;
                    var fi = new FileInfo(originalFileName);

                    // now move the file
                    File.Move(FullServerPath, newFullServerPath);

                    ImportFolderID = newFolderID;
                    FilePath = newPartialPath;
                    var repVids = new VideoLocalRepository();
                    repVids.Save(this);

                    try
                    {
                        // move any subtitle files
                        foreach (var subtitleFile in Utils.GetPossibleSubtitleFiles(originalFileName))
                        {
                            if (File.Exists(subtitleFile))
                            {
                                var fiSub = new FileInfo(subtitleFile);
                                var newSubPath = Path.Combine(Path.GetDirectoryName(newFullServerPath), fiSub.Name);
                                if (File.Exists(newSubPath))
                                {
                                    // if the file already exists, we can just delete the source file instead
                                    // this is safer than deleting and moving
                                    File.Delete(newSubPath);
                                }
                                else
                                    File.Move(subtitleFile, newSubPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                    }

                    // check for any empty folders in drop folder
                    // only for the drop folder
                    if (dropFolder.IsDropSource == 1)
                    {
                        foreach (
                            var folderName in
                                Directory.GetDirectories(dropFolder.ImportFolderLocation, "*",
                                    SearchOption.AllDirectories))
                        {
                            if (Directory.Exists(folderName))
                            {
                                if (Directory.GetFiles(folderName, "*", SearchOption.AllDirectories).Length == 0)
                                {
                                    try
                                    {
                                        Directory.Delete(folderName, true);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.ErrorException(ex.ToString(), ex);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = string.Format("Could not move file: {0} -- {1}", FullServerPath, ex);
                logger.ErrorException(msg, ex);
            }
        }


        public Contract_VideoLocal ToContract(int userID)
        {
            var contract = new Contract_VideoLocal();
            contract.CRC32 = CRC32;
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.FilePath = FilePath;
            contract.FileSize = FileSize;
            contract.Hash = Hash;
            contract.HashSource = HashSource;
            contract.ImportFolder = ImportFolder.ToContract();
            contract.ImportFolderID = ImportFolderID;
            contract.IsIgnored = IsIgnored;
            contract.IsVariation = IsVariation;
            contract.MD5 = MD5;
            contract.SHA1 = SHA1;
            contract.VideoLocalID = VideoLocalID;

            var userRecord = GetUserRecord(userID);
            if (userRecord == null)
            {
                contract.IsWatched = 0;
                contract.WatchedDate = null;
            }
            else
            {
                contract.IsWatched = 1;
                contract.WatchedDate = userRecord.WatchedDate;
            }

            return contract;
        }

        public Contract_VideoDetailed ToContractDetailed(int userID)
        {
            var contract = new Contract_VideoDetailed();

            // get the cross ref episode
            var xrefs = EpisodeCrossRefs;
            if (xrefs.Count == 0) return null;

            contract.Percentage = xrefs[0].Percentage;
            contract.EpisodeOrder = xrefs[0].EpisodeOrder;
            contract.CrossRefSource = xrefs[0].CrossRefSource;
            contract.AnimeEpisodeID = xrefs[0].EpisodeID;

            contract.VideoLocal_FilePath = FilePath;
            contract.VideoLocal_Hash = Hash;
            contract.VideoLocal_FileSize = FileSize;
            contract.VideoLocalID = VideoLocalID;
            contract.VideoLocal_IsIgnored = IsIgnored;
            contract.VideoLocal_IsVariation = IsVariation;

            contract.VideoLocal_MD5 = MD5;
            contract.VideoLocal_SHA1 = SHA1;
            contract.VideoLocal_CRC32 = CRC32;
            contract.VideoLocal_HashSource = HashSource;

            var userRecord = GetUserRecord(userID);
            if (userRecord == null)
                contract.VideoLocal_IsWatched = 0;
            else
                contract.VideoLocal_IsWatched = 1;

            // Import Folder
            var ns = ImportFolder; // to prevent multiple db calls
            if (ns != null)
            {
                contract.ImportFolderID = ns.ImportFolderID;
                contract.ImportFolderLocation = ns.ImportFolderLocation;
                contract.ImportFolderName = ns.ImportFolderName;
            }

            // video info
            var vi = VideoInfo; // to prevent multiple db calls
            if (vi != null)
            {
                contract.VideoInfo_AudioBitrate = vi.AudioBitrate;
                contract.VideoInfo_AudioCodec = vi.AudioCodec;
                contract.VideoInfo_Duration = vi.Duration;
                contract.VideoInfo_VideoBitrate = vi.VideoBitrate;
                contract.VideoInfo_VideoBitDepth = vi.VideoBitDepth;
                contract.VideoInfo_VideoCodec = vi.VideoCodec;
                contract.VideoInfo_VideoFrameRate = vi.VideoFrameRate;
                contract.VideoInfo_VideoResolution = vi.VideoResolution;
                contract.VideoInfo_VideoInfoID = vi.VideoInfoID;
            }

            // AniDB File
            var anifile = GetAniDBFile(); // to prevent multiple db calls
            if (anifile != null)
            {
                contract.AniDB_Anime_GroupName = anifile.Anime_GroupName;
                contract.AniDB_Anime_GroupNameShort = anifile.Anime_GroupNameShort;
                contract.AniDB_AnimeID = anifile.AnimeID;
                contract.AniDB_CRC = anifile.CRC;
                contract.AniDB_Episode_Rating = anifile.Episode_Rating;
                contract.AniDB_Episode_Votes = anifile.Episode_Votes;
                contract.AniDB_File_AudioCodec = anifile.File_AudioCodec;
                contract.AniDB_File_Description = anifile.File_Description;
                contract.AniDB_File_FileExtension = anifile.File_FileExtension;
                contract.AniDB_File_LengthSeconds = anifile.File_LengthSeconds;
                contract.AniDB_File_ReleaseDate = anifile.File_ReleaseDate;
                contract.AniDB_File_Source = anifile.File_Source;
                contract.AniDB_File_VideoCodec = anifile.File_VideoCodec;
                contract.AniDB_File_VideoResolution = anifile.File_VideoResolution;
                contract.AniDB_FileID = anifile.FileID;
                contract.AniDB_GroupID = anifile.GroupID;
                contract.AniDB_MD5 = anifile.MD5;
                contract.AniDB_SHA1 = anifile.SHA1;
                contract.AniDB_File_FileVersion = anifile.FileVersion;

                // languages
                contract.LanguagesAudio = anifile.LanguagesRAW;
                contract.LanguagesSubtitle = anifile.SubtitlesRAW;
            }
            else
            {
                contract.AniDB_Anime_GroupName = "";
                contract.AniDB_Anime_GroupNameShort = "";
                contract.AniDB_CRC = "";
                contract.AniDB_File_AudioCodec = "";
                contract.AniDB_File_Description = "";
                contract.AniDB_File_FileExtension = "";
                contract.AniDB_File_Source = "";
                contract.AniDB_File_VideoCodec = "";
                contract.AniDB_File_VideoResolution = "";
                contract.AniDB_MD5 = "";
                contract.AniDB_SHA1 = "";
                contract.AniDB_File_FileVersion = 1;

                // languages
                contract.LanguagesAudio = "";
                contract.LanguagesSubtitle = "";
            }


            var relGroup = ReleaseGroup; // to prevent multiple db calls
            if (relGroup != null)
                contract.ReleaseGroup = relGroup.ToContract();
            else
                contract.ReleaseGroup = null;

            return contract;
        }

        public Contract_VideoLocalManualLink ToContractManualLink(int userID)
        {
            var contract = new Contract_VideoLocalManualLink();
            contract.CRC32 = CRC32;
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.FilePath = FilePath;
            contract.FileSize = FileSize;
            contract.Hash = Hash;
            contract.HashSource = HashSource;
            contract.ImportFolder = ImportFolder.ToContract();
            contract.ImportFolderID = ImportFolderID;
            contract.IsIgnored = IsIgnored;
            contract.IsVariation = IsVariation;
            contract.MD5 = MD5;
            contract.SHA1 = SHA1;
            contract.VideoLocalID = VideoLocalID;

            var userRecord = GetUserRecord(userID);
            if (userRecord == null)
            {
                contract.IsWatched = 0;
                contract.WatchedDate = null;
            }
            else
            {
                contract.IsWatched = 1;
                contract.WatchedDate = userRecord.WatchedDate;
            }

            return contract;
        }
    }
}