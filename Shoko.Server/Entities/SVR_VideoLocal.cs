using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AniDBAPI;
using FluentNHibernate.Utils;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.MAL;
using Shoko.Server.LZ4;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Stream = Shoko.Models.PlexAndKodi.Stream;

using Path = Pri.LongPath.Path;

namespace Shoko.Server.Entities
{
    public class SVR_VideoLocal : VideoLocal, IHash
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public SVR_VideoLocal()
        { }
        #region DB columns

        public int MediaVersion { get; set; }
        public byte[] MediaBlob { get; set; }
        public int MediaSize { get; set; }

        #endregion



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

        public List<SVR_VideoLocal_Place> Places => RepoFactory.VideoLocalPlace.GetByVideoLocal(VideoLocalID);


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


        public SVR_AniDB_File GetAniDBFile()
        {
            return RepoFactory.AniDB_File.GetByHash(Hash);
        }


        public VideoLocal_User GetUserRecord(int userID)
        {
            return RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, VideoLocalID);
        }

        public SVR_AniDB_ReleaseGroup ReleaseGroup
        {
            get
            {
                SVR_AniDB_File anifile = GetAniDBFile();
                if (anifile == null) return null;

                return RepoFactory.AniDB_ReleaseGroup.GetByGroupID(anifile.GroupID);
            }
        }

        public List<SVR_AnimeEpisode> GetAnimeEpisodes()
        {
            return RepoFactory.AnimeEpisode.GetByHash(Hash);
        }



        public List<SVR_CrossRef_File_Episode> EpisodeCrossRefs
        {
            get
            {
                if (Hash.Length == 0) return new List<SVR_CrossRef_File_Episode>();

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
            Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(fullname);
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
            foreach (SVR_VideoLocal_Place p in Places.OrderBy(a => a.ImportFolderType))
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
        public SVR_VideoLocal_Place GetBestVideoLocalPlace()
        {
            foreach (SVR_VideoLocal_Place p in Places.OrderBy(a => a.ImportFolderType))
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

            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null) return;

            List<SVR_JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();

            // update the video file to watched
            int mywatched = watched ? 1 : 0;

            if (user.IsAniDBUser == 0)
                SaveWatchedStatus(watched, userID, watchedDate, updateWatchedDate);
            else
            {
                // if the user is AniDB user we also want to update any other AniDB
                // users to keep them in sync
                foreach (SVR_JMMUser juser in aniDBUsers)
                {
                    if (juser.IsAniDBUser == 1)
                        SaveWatchedStatus(watched, juser.JMMUserID, watchedDate, updateWatchedDate);
                }
            }


            // now lets find all the associated AniDB_File record if there is one
            if (user.IsAniDBUser == 1)
            {
                SVR_AniDB_File aniFile = RepoFactory.AniDB_File.GetByHash(this.Hash);
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
                            watchedDate.HasValue ? AniDB.GetAniDBDateAsSeconds(watchedDate) : 0);
                        cmd.Save();
                    }
                }
            }

            // now find all the episode records associated with this video file
            // but we also need to check if theer are any other files attached to this episode with a watched
            // status, 


            SVR_AnimeSeries ser = null;
            // get all files associated with this episode
            List<SVR_CrossRef_File_Episode> xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(this.Hash);
            Dictionary<int, SVR_AnimeSeries> toUpdateSeries = new Dictionary<int, SVR_AnimeSeries>();
            if (watched)
            {
                // find the total watched percentage
                // eg one file can have a % = 100
                // or if 2 files make up one episodes they will each have a % = 50

                foreach (SVR_CrossRef_File_Episode xref in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID);
                    // get all the files for this episode
                    int epPercentWatched = 0;
                    foreach (SVR_CrossRef_File_Episode filexref in ep.FileCrossRefs)
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
                            foreach (SVR_JMMUser juser in aniDBUsers)
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
                foreach (SVR_CrossRef_File_Episode xrefEp in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xrefEp.EpisodeID);
                    ser = ep.GetAnimeSeries();
                    if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                        toUpdateSeries.Add(ser.AnimeSeriesID, ser);
                    // get all the files for this episode
                    int epPercentWatched = 0;
                    foreach (SVR_CrossRef_File_Episode filexref in ep.FileCrossRefs)
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
                            foreach (SVR_JMMUser juser in aniDBUsers)
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
                foreach (SVR_AnimeSeries s in toUpdateSeries.Values)
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



        public CL_VideoLocal ToClient(int userID)
        {
            CL_VideoLocal cl = new CL_VideoLocal();
            cl.CRC32 = this.CRC32;
            cl.DateTimeUpdated = this.DateTimeUpdated;
            cl.FileName = this.FileName;
            cl.FileSize = this.FileSize;
            cl.Hash = this.Hash;
            cl.HashSource = this.HashSource;
            cl.IsIgnored = this.IsIgnored;
            cl.IsVariation = this.IsVariation;
            cl.Duration = this.Duration;
            cl.MD5 = this.MD5;
            cl.SHA1 = this.SHA1;
            cl.VideoLocalID = this.VideoLocalID;
            cl.Places = Places.Select(a => a.ToContract()).ToList();
            VideoLocal_User userRecord = this.GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                cl.IsWatched = 0;
                cl.WatchedDate = null;
            }
            else
            {
                cl.IsWatched = 1;
                cl.WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord != null)
                cl.ResumePosition = userRecord.ResumePosition;
            cl.Media = GetMediaFromUser(userID);           
            return cl;
        }
        private static Regex UrlSafe = new Regex("[ \\$^`:<>\\[\\]\\{\\}\"“\\+%@/;=\\?\\\\\\^\\|~‘,]", RegexOptions.Compiled);
        private static Regex UrlSafe2 = new Regex("[^0-9a-zA-Z_\\.\\s]", RegexOptions.Compiled);
        public Media GetMediaFromUser(int userID)
        {
            Media n = null;
            if (Media == null)
            {
                SVR_VideoLocal_Place pl = GetBestVideoLocalPlace();
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
                        p.Key = ((IProvider)null).ReplaceSchemeHost(((IProvider)null).ConstructVideoLocalStream(userID, VideoLocalID.ToString(), name, false));
                        if (p.Streams != null)
                        {
                            foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == "3"))
                            {
                                s.Key = ((IProvider)null).ReplaceSchemeHost(((IProvider)null).ConstructFileStream(userID, s.File, false));
                            }
                        }
                    }
                }
            }
            return n;
        }
        public CL_VideoDetailed ToClientDetailed(int userID)
        {
            CL_VideoDetailed cl = new CL_VideoDetailed();

            // get the cross ref episode
            List<SVR_CrossRef_File_Episode> xrefs = this.EpisodeCrossRefs;
            if (xrefs.Count == 0) return null;

            cl.Percentage = xrefs[0].Percentage;
            cl.EpisodeOrder = xrefs[0].EpisodeOrder;
            cl.CrossRefSource = xrefs[0].CrossRefSource;
            cl.AnimeEpisodeID = xrefs[0].EpisodeID;

            cl.VideoLocal_FileName = this.FileName;
            cl.VideoLocal_Hash = this.Hash;
            cl.VideoLocal_FileSize = this.FileSize;
            cl.VideoLocalID = this.VideoLocalID;
            cl.VideoLocal_IsIgnored = this.IsIgnored;
            cl.VideoLocal_IsVariation = this.IsVariation;
            cl.Places = Places.Select(a => a.ToContract()).ToList();

            cl.VideoLocal_MD5 = this.MD5;
            cl.VideoLocal_SHA1 = this.SHA1;
            cl.VideoLocal_CRC32 = this.CRC32;
            cl.VideoLocal_HashSource = this.HashSource;

            VideoLocal_User userRecord = this.GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                cl.VideoLocal_IsWatched = 0;
                cl.VideoLocal_WatchedDate = null;
                cl.VideoLocal_ResumePosition = 0;
            }
            else
            {
                cl.VideoLocal_IsWatched = userRecord.WatchedDate.HasValue ? 1 : 0;
                cl.VideoLocal_WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord!=null)
                cl.VideoLocal_ResumePosition = userRecord.ResumePosition;
            cl.VideoInfo_AudioBitrate = AudioBitrate;
            cl.VideoInfo_AudioCodec = AudioCodec;
            cl.VideoInfo_Duration = Duration;
            cl.VideoInfo_VideoBitrate = VideoBitrate;
            cl.VideoInfo_VideoBitDepth = VideoBitDepth;
            cl.VideoInfo_VideoCodec = VideoCodec;
            cl.VideoInfo_VideoFrameRate = VideoFrameRate;
            cl.VideoInfo_VideoResolution = VideoResolution;

            // AniDB File
            SVR_AniDB_File anifile = this.GetAniDBFile(); // to prevent multiple db calls
            if (anifile != null)
            {
                cl.AniDB_Anime_GroupName = anifile.Anime_GroupName;
                cl.AniDB_Anime_GroupNameShort = anifile.Anime_GroupNameShort;
                cl.AniDB_AnimeID = anifile.AnimeID;
                cl.AniDB_CRC = anifile.CRC;
                cl.AniDB_Episode_Rating = anifile.Episode_Rating;
                cl.AniDB_Episode_Votes = anifile.Episode_Votes;
                cl.AniDB_File_AudioCodec = anifile.File_AudioCodec;
                cl.AniDB_File_Description = anifile.File_Description;
                cl.AniDB_File_FileExtension = anifile.File_FileExtension;
                cl.AniDB_File_LengthSeconds = anifile.File_LengthSeconds;
                cl.AniDB_File_ReleaseDate = anifile.File_ReleaseDate;
                cl.AniDB_File_Source = anifile.File_Source;
                cl.AniDB_File_VideoCodec = anifile.File_VideoCodec;
                cl.AniDB_File_VideoResolution = anifile.File_VideoResolution;
                cl.AniDB_FileID = anifile.FileID;
                cl.AniDB_GroupID = anifile.GroupID;
                cl.AniDB_MD5 = anifile.MD5;
                cl.AniDB_SHA1 = anifile.SHA1;
                cl.AniDB_File_FileVersion = anifile.FileVersion;

                // languages
                cl.LanguagesAudio = anifile.LanguagesRAW;
                cl.LanguagesSubtitle = anifile.SubtitlesRAW;
            }
            else
            {
                cl.AniDB_Anime_GroupName = "";
                cl.AniDB_Anime_GroupNameShort = "";
                cl.AniDB_CRC = "";
                cl.AniDB_File_AudioCodec = "";
                cl.AniDB_File_Description = "";
                cl.AniDB_File_FileExtension = "";
                cl.AniDB_File_Source = "";
                cl.AniDB_File_VideoCodec = "";
                cl.AniDB_File_VideoResolution = "";
                cl.AniDB_MD5 = "";
                cl.AniDB_SHA1 = "";
                cl.AniDB_File_FileVersion = 1;

                // languages
                cl.LanguagesAudio = "";
                cl.LanguagesSubtitle = "";
            }


            SVR_AniDB_ReleaseGroup relGroup = this.ReleaseGroup; // to prevent multiple db calls
            if (relGroup != null)
                cl.ReleaseGroup = relGroup;
            else
                cl.ReleaseGroup = null;
            cl.Media = GetMediaFromUser(userID);
            return cl;
        }

        public CL_VideoLocal_ManualLink ToContractManualLink(int userID)
        {
            CL_VideoLocal_ManualLink cl = new CL_VideoLocal_ManualLink();
            cl.CRC32 = this.CRC32;
            cl.DateTimeUpdated = this.DateTimeUpdated;
            cl.FileName = this.FileName;
            cl.FileSize = this.FileSize;
            cl.Hash = this.Hash;
            cl.HashSource = this.HashSource;
            cl.IsIgnored = this.IsIgnored;
            cl.IsVariation = this.IsVariation;
            cl.MD5 = this.MD5;
            cl.SHA1 = this.SHA1;
            cl.VideoLocalID = this.VideoLocalID;
            cl.Places = Places.Select(a => a.ToContract()).ToList();

            VideoLocal_User userRecord = this.GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                cl.IsWatched = 0;
                cl.WatchedDate = null;
                cl.ResumePosition = 0;
            }
            else
            {
                cl.IsWatched = userRecord.WatchedDate.HasValue ? 1 : 0;
                cl.WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord!=null)
                cl.ResumePosition = userRecord.ResumePosition;
            return cl;
        }
    }
}