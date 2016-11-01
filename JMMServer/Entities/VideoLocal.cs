using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AniDBAPI;

using FluentNHibernate.Utils;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Commands;
using JMMServer.Commands.MAL;
using JMMServer.LZ4;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using NHibernate;
using NLog;
using NutzCode.CloudFileSystem;
using Stream = JMMContracts.PlexAndKodi.Stream;

using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using File = Pri.LongPath.File;
using FileInfo = Pri.LongPath.FileInfo;
using JMMServer.Repositories.NHibernate;

namespace JMMServer.Entities
{
    public class VideoLocal : IHash
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int VideoLocalID { get; private set; }
        public string Hash { get; set; }
        public string CRC32 { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public int HashSource { get; set; }
        public long FileSize { get; set; }
        public int IsIgnored { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public int IsVariation { get; set; }
        public int MediaVersion { get; set; }
        public byte[] MediaBlob { get; set; }
        public int MediaSize { get; set; }

        public string VideoCodec { get; set; } = string.Empty;
        public string VideoBitrate { get; set; } = string.Empty;
        public string VideoBitDepth { get; set; } = string.Empty;
        public string VideoFrameRate { get; set; } = string.Empty;
        public string VideoResolution { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;
        public string AudioBitrate { get; set; } = string.Empty;
        public long Duration { get; set; }
        public string FileName { get; set; }

        public string Info => string.IsNullOrEmpty(FileName) ? string.Empty : FileName;



        public const int MEDIA_VERSION = 2;


        internal Media _media = null;

        public virtual Media Media
        {
            get
            {
                if ((_media == null) && (MediaBlob != null) && (MediaBlob.Length > 0) && (MediaSize > 0))
                    _media = CompressionHelper.DeserializeObject<Media>(MediaBlob, MediaSize);
                return _media;
            }
            set
            {
                _media = value;
                int outsize;
                MediaBlob = CompressionHelper.SerializeObject(value, out outsize);
                MediaSize = outsize;
                MediaVersion = MEDIA_VERSION;
            }
        }

        public List<VideoLocal_Place> Places => RepoFactory.VideoLocalPlace.GetByVideoLocal(VideoLocalID);


        public void CollectContractMemory()
        {
            _media = null;
        }

        public string ToStringDetailed()
        {
            StringBuilder sb = new StringBuilder("");
            sb.Append(Environment.NewLine);
            sb.Append("VideoLocalID: " + VideoLocalID.ToString());

            sb.Append(Environment.NewLine);
            sb.Append("FileName: " + FileName);
/*            sb.Append(Environment.NewLine);
            sb.Append("ImportFolderID: " + ImportFolderID.ToString());*/
            sb.Append(Environment.NewLine);
            sb.Append("Hash: " + Hash);
            sb.Append(Environment.NewLine);
            sb.Append("FileSize: " + FileSize.ToString());
            sb.Append(Environment.NewLine);
            /*
            try
            {
                if (ImportFolder != null)
                    sb.Append("ImportFolderLocation: " + ImportFolder.ImportFolderLocation);
            }
            catch (Exception ex)
            {
                sb.Append("ImportFolderLocation: " + ex.ToString());
            }

            sb.Append(Environment.NewLine);
            */
            return sb.ToString();
        }

        public string ED2KHash
        {
            get { return Hash; }
            set { Hash = value; }
        }


        public AniDB_File GetAniDBFile()
        {
            return RepoFactory.AniDB_File.GetByHash(Hash);
        }


        public VideoLocal_User GetUserRecord(int userID)
        {
            return RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, VideoLocalID);
        }

        public AniDB_ReleaseGroup ReleaseGroup
        {
            get
            {
                AniDB_File anifile = GetAniDBFile();
                if (anifile == null) return null;

                return RepoFactory.AniDB_ReleaseGroup.GetByGroupID(anifile.GroupID);
            }
        }

        public List<AnimeEpisode> GetAnimeEpisodes()
        {
            return RepoFactory.AnimeEpisode.GetByHash(Hash);
        }



        public List<CrossRef_File_Episode> EpisodeCrossRefs
        {
            get
            {
                if (Hash.Length == 0) return new List<CrossRef_File_Episode>();

                return RepoFactory.CrossRef_File_Episode.GetByHash(Hash);
            }
        }

        private void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            VideoLocal_User vidUserRecord = GetUserRecord(userID);
            if (watched)
            {
                if (vidUserRecord == null)
                    vidUserRecord = new VideoLocal_User();
                vidUserRecord.WatchedDate = DateTime.Now;
                vidUserRecord.JMMUserID = userID;
                vidUserRecord.VideoLocalID = this.VideoLocalID;

                if (watchedDate.HasValue)
                {
                    if (updateWatchedDate)
                        vidUserRecord.WatchedDate = watchedDate.Value;
                }

                RepoFactory.VideoLocalUser.Save(vidUserRecord);
            }
            else
            {
                if (vidUserRecord != null)
                {
                    vidUserRecord.WatchedDate = null;
                    RepoFactory.VideoLocalUser.Save(vidUserRecord);
                }
            }
        }

        public static IFile ResolveFile(string fullname)
        {
            Tuple<ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(fullname);
            IFileSystem fs = tup?.Item1.FileSystem;
            if (fs == null)
                return null;
            try
            {
                FileSystemResult<IObject> fobj = fs?.Resolve(fullname);
                if (!fobj.IsOk || fobj.Result is IDirectory)
                {
                    logger.Warn("File not found: " + fullname);
                    return null;

                }
                return fobj.Result as IFile;
            }
            catch (Exception)
            {
                logger.Warn("File with Exception: " + fullname);
                throw;
            }
        }
        public IFile GetBestFileLink()
        {
            IFile file=null;
            foreach (VideoLocal_Place p in Places.OrderBy(a => a.ImportFolderType))
            {
                if (p != null)
                {
                    file = ResolveFile(p.FullServerPath);
                    if (file != null)
                        break;
                }
            }
            return file;
        }
        public VideoLocal_Place GetBestVideoLocalPlace()
        {
            foreach (VideoLocal_Place p in Places.OrderBy(a => a.ImportFolderType))
            {
                if (p != null)
                {
                    if (ResolveFile(p.FullServerPath) != null)
                        return p;
                }
            }
            return null;
        }

        public void SetResumePosition(long resumeposition, int userID)
        {
            VideoLocal_User vuser = GetUserRecord(userID);
            if (vuser == null)
            {
                vuser=new VideoLocal_User();
                vuser.JMMUserID = userID;
                vuser.VideoLocalID = VideoLocalID;
                vuser.ResumePosition = resumeposition;
            }
            RepoFactory.VideoLocalUser.Save(vuser);
        }

        public void ToggleWatchedStatus(bool watched, int userID)
        {
            ToggleWatchedStatus(watched, true, null, true, true, userID, true, true);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
            bool updateStatsCache, int userID,
            bool syncTrakt, bool updateWatchedDate)
        {

            JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null) return;

            List<JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();

            // update the video file to watched
            int mywatched = watched ? 1 : 0;

            if (user.IsAniDBUser == 0)
                SaveWatchedStatus(watched, userID, watchedDate, updateWatchedDate);
            else
            {
                // if the user is AniDB user we also want to update any other AniDB
                // users to keep them in sync
                foreach (JMMUser juser in aniDBUsers)
                {
                    if (juser.IsAniDBUser == 1)
                        SaveWatchedStatus(watched, juser.JMMUserID, watchedDate, updateWatchedDate);
                }
            }


            // now lets find all the associated AniDB_File record if there is one
            if (user.IsAniDBUser == 1)
            {
                AniDB_File aniFile = RepoFactory.AniDB_File.GetByHash(this.Hash);
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


                    RepoFactory.AniDB_File.Save(aniFile, false);
                }

                if (updateOnline)
                {
                    if ((watched && ServerSettings.AniDB_MyList_SetWatched) ||
                        (!watched && ServerSettings.AniDB_MyList_SetUnwatched))
                    {
                        CommandRequest_UpdateMyListFileStatus cmd = new CommandRequest_UpdateMyListFileStatus(
                            this.Hash, watched, false,
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
            List<CrossRef_File_Episode> xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(this.Hash);
            Dictionary<int, AnimeSeries> toUpdateSeries = new Dictionary<int, AnimeSeries>();
            if (watched)
            {
                // find the total watched percentage
                // eg one file can have a % = 100
                // or if 2 files make up one episodes they will each have a % = 50

                foreach (CrossRef_File_Episode xref in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID);
                    // get all the files for this episode
                    int epPercentWatched = 0;
                    foreach (CrossRef_File_Episode filexref in ep.FileCrossRefs)
                    {
                        VideoLocal_User vidUser = filexref.GetVideoLocalUserRecord(userID);
                        if (vidUser != null && vidUser.WatchedDate.HasValue)
                        {
                            // if not null means it is watched
                            epPercentWatched += filexref.Percentage;
                        }

                        if (epPercentWatched > 95) break;
                    }

                    if (epPercentWatched > 95)
                    {
                        ser = ep.GetAnimeSeries();
                        if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                            toUpdateSeries.Add(ser.AnimeSeriesID, ser);
                        if (user.IsAniDBUser == 0)
                            ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
                        else
                        {
                            // if the user is AniDB user we also want to update any other AniDB
                            // users to keep them in sync
                            foreach (JMMUser juser in aniDBUsers)
                            {
                                if (juser.IsAniDBUser == 1)
                                    ep.SaveWatchedStatus(true, juser.JMMUserID, watchedDate, updateWatchedDate);
                            }
                        }

                        if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                            !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                        {
                            CommandRequest_TraktHistoryEpisode cmdSyncTrakt =
                                new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Add);
                            cmdSyncTrakt.Save();
                        }

                        if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                            !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                        {
                            CommandRequest_MALUpdatedWatchedStatus cmdMAL =
                                new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                            cmdMAL.Save();
                        }
                    }
                }
            }
            else
            {
                // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
                foreach (CrossRef_File_Episode xrefEp in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xrefEp.EpisodeID);
                    ser = ep.GetAnimeSeries();
                    if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                        toUpdateSeries.Add(ser.AnimeSeriesID, ser);
                    // get all the files for this episode
                    int epPercentWatched = 0;
                    foreach (CrossRef_File_Episode filexref in ep.FileCrossRefs)
                    {
                        VideoLocal_User vidUser = filexref.GetVideoLocalUserRecord(userID);
                        if (vidUser != null && vidUser.WatchedDate.HasValue)
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
                            foreach (JMMUser juser in aniDBUsers)
                            {
                                if (juser.IsAniDBUser == 1)
                                    ep.SaveWatchedStatus(false, juser.JMMUserID, watchedDate, true);
                            }
                        }

                        if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                            !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                        {
                            CommandRequest_TraktHistoryEpisode cmdSyncTrakt =
                                new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Remove);
                            cmdSyncTrakt.Save();
                        }
                    }
                }
                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                    !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    CommandRequest_MALUpdatedWatchedStatus cmdMAL =
                        new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                    cmdMAL.Save();
                }
            }


            // update stats for groups and series
            if (toUpdateSeries.Count > 0 && updateStats)
            {
                foreach (AnimeSeries s in toUpdateSeries.Values)
                    // update all the groups above this series in the heirarchy
                    s.UpdateStats(true, true, true);
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
            }

            //if (ser != null && updateStatsCache)
            //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
        }

        public override string ToString()
        {
            return string.Format("{0} --- {1}", FileName, Hash);
        }



        public Contract_VideoLocal ToContract(int userID)
        {
            Contract_VideoLocal contract = new Contract_VideoLocal();
            contract.CRC32 = this.CRC32;
            contract.DateTimeUpdated = this.DateTimeUpdated;
            contract.FileName = this.FileName;
            contract.FileSize = this.FileSize;
            contract.Hash = this.Hash;
            contract.HashSource = this.HashSource;
            contract.IsIgnored = this.IsIgnored;
            contract.IsVariation = this.IsVariation;
            contract.Duration = this.Duration;
            contract.MD5 = this.MD5;
            contract.SHA1 = this.SHA1;
            contract.VideoLocalID = this.VideoLocalID;
            contract.Places = Places.Select(a => a.ToContract()).ToList();
            VideoLocal_User userRecord = this.GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                contract.IsWatched = 0;
                contract.WatchedDate = null;
            }
            else
            {
                contract.IsWatched = 1;
                contract.WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord != null)
                contract.ResumePosition = userRecord.ResumePosition;
            contract.Media = GetMediaFromUser(userID);           
            return contract;
        }
        private static Regex UrlSafe = new Regex("[ \\$^`:<>\\[\\]\\{\\}\"“\\+%@/;=\\?\\\\\\^\\|~‘,]", RegexOptions.Compiled);
        private static Regex UrlSafe2 = new Regex("[^0-9a-zA-Z_\\.\\s]", RegexOptions.Compiled);
        public Media GetMediaFromUser(int userID)
        {
            Media n = null;
            if (Media == null)
            {
                VideoLocal_Place pl = GetBestVideoLocalPlace();
                if (pl != null)
                {
                    IFileSystem f = pl.ImportFolder.FileSystem;
                    FileSystemResult<IObject> src = f.Resolve(pl.FullServerPath);
                    if (src != null && src.IsOk && src.Result is IFile)
                    {
                        if (pl.RefreshMediaInfo())
                        {
                            RepoFactory.VideoLocal.Save(pl.VideoLocal,true);
                        }
                    }
                }
            }
            if (Media != null)
            {

                n = Media.DeepClone();
                if (n?.Parts != null)
                {
                    foreach (Part p in n?.Parts)
                    {
                        string name = UrlSafe.Replace(Path.GetFileName(FileName)," ").Replace("  "," ").Replace("  "," ").Trim();
                        name = UrlSafe2.Replace(name, string.Empty).Trim().Replace("..",".").Replace("..",".").Replace("__","_").Replace("__","_").Replace(" ", "_").Replace("_.",".");
                        while (name.StartsWith("_"))
                            name = name.Substring(1);
                        while (name.StartsWith("."))
                            name = name.Substring(1);
                        p.Key = PlexAndKodi.Helper.ReplaceSchemeHost(PlexAndKodi.Helper.ConstructVideoLocalStream(userID, VideoLocalID.ToString(), name, false));
                        if (p.Streams != null)
                        {
                            foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == "3"))
                            {
                                s.Key = PlexAndKodi.Helper.ReplaceSchemeHost(PlexAndKodi.Helper.ConstructFileStream(userID, s.File, false));
                            }
                        }
                    }
                }
            }
            return n;
        }
        public Contract_VideoDetailed ToContractDetailed(int userID)
        {
            Contract_VideoDetailed contract = new Contract_VideoDetailed();

            // get the cross ref episode
            List<CrossRef_File_Episode> xrefs = this.EpisodeCrossRefs;
            if (xrefs.Count == 0) return null;

            contract.Percentage = xrefs[0].Percentage;
            contract.EpisodeOrder = xrefs[0].EpisodeOrder;
            contract.CrossRefSource = xrefs[0].CrossRefSource;
            contract.AnimeEpisodeID = xrefs[0].EpisodeID;

            contract.VideoLocal_FileName = this.FileName;
            contract.VideoLocal_Hash = this.Hash;
            contract.VideoLocal_FileSize = this.FileSize;
            contract.VideoLocalID = this.VideoLocalID;
            contract.VideoLocal_IsIgnored = this.IsIgnored;
            contract.VideoLocal_IsVariation = this.IsVariation;
            contract.Places = Places.Select(a => a.ToContract()).ToList();

            contract.VideoLocal_MD5 = this.MD5;
            contract.VideoLocal_SHA1 = this.SHA1;
            contract.VideoLocal_CRC32 = this.CRC32;
            contract.VideoLocal_HashSource = this.HashSource;

            VideoLocal_User userRecord = this.GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                contract.VideoLocal_IsWatched = 0;
                contract.VideoLocal_WatchedDate = null;
                contract.VideoLocal_ResumePosition = 0;
            }
            else
            {
                contract.VideoLocal_IsWatched = userRecord.WatchedDate.HasValue ? 1 : 0;
                contract.VideoLocal_WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord!=null)
                contract.VideoLocal_ResumePosition = userRecord.ResumePosition;
            contract.VideoInfo_AudioBitrate = AudioBitrate;
            contract.VideoInfo_AudioCodec = AudioCodec;
            contract.VideoInfo_Duration = Duration;
            contract.VideoInfo_VideoBitrate = VideoBitrate;
            contract.VideoInfo_VideoBitDepth = VideoBitDepth;
            contract.VideoInfo_VideoCodec = VideoCodec;
            contract.VideoInfo_VideoFrameRate = VideoFrameRate;
            contract.VideoInfo_VideoResolution = VideoResolution;

            // AniDB File
            AniDB_File anifile = this.GetAniDBFile(); // to prevent multiple db calls
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


            AniDB_ReleaseGroup relGroup = this.ReleaseGroup; // to prevent multiple db calls
            if (relGroup != null)
                contract.ReleaseGroup = relGroup.ToContract();
            else
                contract.ReleaseGroup = null;
            contract.Media = GetMediaFromUser(userID);
            return contract;
        }

        public Contract_VideoLocalManualLink ToContractManualLink(int userID)
        {
            Contract_VideoLocalManualLink contract = new Contract_VideoLocalManualLink();
            contract.CRC32 = this.CRC32;
            contract.DateTimeUpdated = this.DateTimeUpdated;
            contract.FileName = this.FileName;
            contract.FileSize = this.FileSize;
            contract.Hash = this.Hash;
            contract.HashSource = this.HashSource;
            contract.IsIgnored = this.IsIgnored;
            contract.IsVariation = this.IsVariation;
            contract.MD5 = this.MD5;
            contract.SHA1 = this.SHA1;
            contract.VideoLocalID = this.VideoLocalID;
            contract.Places = Places.Select(a => a.ToContract()).ToList();

            VideoLocal_User userRecord = this.GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                contract.IsWatched = 0;
                contract.WatchedDate = null;
                contract.ResumePosition = 0;
            }
            else
            {
                contract.IsWatched = userRecord.WatchedDate.HasValue ? 1 : 0;
                contract.WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord!=null)
                contract.ResumePosition = userRecord.ResumePosition;
            return contract;
        }
    }
}