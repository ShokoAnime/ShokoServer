using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using NLog;
using NutzCode.CloudFileSystem;
using Pri.LongPath;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Models
{
    public class SVR_VideoLocal : VideoLocal, IHash
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        #region DB columns

        public int MediaVersion { get; set; }
        public byte[] MediaBlob { get; set; }
        public int MediaSize { get; set; }

        #endregion


        public int MyListID { get; set; }

        [ScriptIgnore]
        public string Info => string.IsNullOrEmpty(FileName) ? string.Empty : FileName;


        public const int MEDIA_VERSION = 3;


        internal Media _media;

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
                MediaBlob = CompressionHelper.SerializeObject(value, out int outsize);
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
            sb.Append("VideoLocalID: " + VideoLocalID);

            sb.Append(Environment.NewLine);
            sb.Append("FileName: " + FileName);
            sb.Append(Environment.NewLine);
            sb.Append("Hash: " + Hash);
            sb.Append(Environment.NewLine);
            sb.Append("FileSize: " + FileSize);
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        [ScriptIgnore]
        public string ED2KHash
        {
            get => Hash;
            set => Hash = value;
        }


        public SVR_AniDB_File GetAniDBFile()
        {
            return RepoFactory.AniDB_File.GetByHash(Hash);
        }


        public VideoLocal_User GetUserRecord(int userID)
        {
            return RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, VideoLocalID);
        }


        internal AniDB_ReleaseGroup ReleaseGroup
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
                vidUserRecord.VideoLocalID = VideoLocalID;

                if (watchedDate.HasValue && updateWatchedDate) vidUserRecord.WatchedDate = watchedDate.Value;

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
            if (string.IsNullOrEmpty(fullname)) return null;
            Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(fullname);
            IFileSystem fs = tup?.Item1?.FileSystem;
            if (fs == null)
                return null;
            try
            {
                FileSystemResult<IObject> fobj = fs.Resolve(fullname);
                if (fobj == null || !fobj.IsOk || fobj.Result is IDirectory) return null;
                return fobj.Result as IFile;
            }
            catch (Exception)
            {
                logger.Warn("File with Exception: " + fullname);
                return null;
            }
        }

        public IFile GetBestFileLink()
        {
            IFile file = null;
            foreach (SVR_VideoLocal_Place p in Places.OrderBy(a => a.ImportFolderType))
            {
                file = ResolveFile(p.FullServerPath);
                if (file != null)
                    break;
            }
            return file;
        }

        public SVR_VideoLocal_Place GetBestVideoLocalPlace(bool resolve = false)
        {
            if (!resolve)
                return Places.Where(p => !string.IsNullOrEmpty(p?.FullServerPath)).OrderBy(a => a.ImportFolderType)
                    .FirstOrDefault();

            return Places.Where(p => !string.IsNullOrEmpty(p?.FullServerPath)).OrderBy(a => a.ImportFolderType)
                .FirstOrDefault(p => ResolveFile(p.FullServerPath) != null);
        }

        public void SetResumePosition(long resumeposition, int userID)
        {
            VideoLocal_User vuser = GetUserRecord(userID);
            if (vuser == null)
                vuser = new VideoLocal_User
                {
                    JMMUserID = userID,
                    VideoLocalID = VideoLocalID,
                    ResumePosition = resumeposition
                };
            else
                vuser.ResumePosition = resumeposition;
            RepoFactory.VideoLocalUser.Save(vuser);
        }

        public void ToggleWatchedStatus(bool watched, int userID)
        {
            ToggleWatchedStatus(watched, true, null, true, userID, true, true);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID,
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
                foreach (SVR_JMMUser juser in aniDBUsers)
                    if (juser.IsAniDBUser == 1)
                        SaveWatchedStatus(watched, juser.JMMUserID, watchedDate, updateWatchedDate);


            // now lets find all the associated AniDB_File record if there is one
            if (user.IsAniDBUser == 1)
            {
                SVR_AniDB_File aniFile = RepoFactory.AniDB_File.GetByHash(Hash);
                if (aniFile != null)
                {
                    aniFile.IsWatched = mywatched;

                    if (watched)
                        aniFile.WatchedDate = watchedDate ?? DateTime.Now;
                    else
                        aniFile.WatchedDate = null;


                    RepoFactory.AniDB_File.Save(aniFile, false);
                }

                if (updateOnline)
                    if ((watched && ServerSettings.AniDB_MyList_SetWatched) ||
                        (!watched && ServerSettings.AniDB_MyList_SetUnwatched))
                    {
                        CommandRequest_UpdateMyListFileStatus cmd = new CommandRequest_UpdateMyListFileStatus(
                            Hash, watched, false,
                            AniDB.GetAniDBDateAsSeconds(watchedDate?.ToUniversalTime()));
                        cmd.Save();
                    }
            }

            // now find all the episode records associated with this video file
            // but we also need to check if theer are any other files attached to this episode with a watched
            // status, 


            SVR_AnimeSeries ser = null;
            // get all files associated with this episode
            List<CrossRef_File_Episode> xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(Hash);
            Dictionary<int, SVR_AnimeSeries> toUpdateSeries = new Dictionary<int, SVR_AnimeSeries>();
            if (watched)
            {
                // find the total watched percentage
                // eg one file can have a % = 100
                // or if 2 files make up one episodes they will each have a % = 50

                foreach (CrossRef_File_Episode xref in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID);
                    // a show we don't have
                    if (ep == null) continue;

                    // get all the files for this episode
                    int epPercentWatched = 0;
                    foreach (CrossRef_File_Episode filexref in ep.FileCrossRefs)
                    {
                        VideoLocal_User vidUser = filexref.GetVideoLocalUserRecord(userID);
                        if (vidUser?.WatchedDate != null)
                            epPercentWatched += filexref.Percentage;

                        if (epPercentWatched > 95) break;
                    }

                    if (epPercentWatched > 95)
                    {
                        ser = ep.GetAnimeSeries();
                        // a problem
                        if (ser == null) continue;
                        if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                            toUpdateSeries.Add(ser.AnimeSeriesID, ser);
                        if (user.IsAniDBUser == 0)
                            ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
                        else
                            foreach (SVR_JMMUser juser in aniDBUsers)
                                if (juser.IsAniDBUser == 1)
                                    ep.SaveWatchedStatus(true, juser.JMMUserID, watchedDate, updateWatchedDate);

                        if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                            !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                        {
                            CommandRequest_TraktHistoryEpisode cmdSyncTrakt =
                                new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Add);
                            cmdSyncTrakt.Save();
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
                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xrefEp.EpisodeID);
                    // a show we don't have
                    if (ep == null) continue;

                    // get all the files for this episode
                    int epPercentWatched = 0;
                    foreach (CrossRef_File_Episode filexref in ep.FileCrossRefs)
                    {
                        VideoLocal_User vidUser = filexref.GetVideoLocalUserRecord(userID);
                        if (vidUser?.WatchedDate != null)
                            epPercentWatched += filexref.Percentage;

                        if (epPercentWatched > 95) break;
                    }

                    if (epPercentWatched < 95)
                    {
                        if (user.IsAniDBUser == 0)
                            ep.SaveWatchedStatus(false, userID, watchedDate, true);
                        else
                            foreach (SVR_JMMUser juser in aniDBUsers)
                                if (juser.IsAniDBUser == 1)
                                    ep.SaveWatchedStatus(false, juser.JMMUserID, watchedDate, true);

                        ser = ep.GetAnimeSeries();
                        // a problem
                        if (ser == null) continue;
                        if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                            toUpdateSeries.Add(ser.AnimeSeriesID, ser);

                        if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                            !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                        {
                            CommandRequest_TraktHistoryEpisode cmdSyncTrakt =
                                new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Remove);
                            cmdSyncTrakt.Save();
                        }
                    }
                }
            }


            // update stats for groups and series
            if (toUpdateSeries.Count > 0 && updateStats)
                foreach (SVR_AnimeSeries s in toUpdateSeries.Values)
                    // update all the groups above this series in the heirarchy
                    s.UpdateStats(true, true, true);
        }

        public override string ToString()
        {
            return $"{FileName} --- {Hash}";
        }


        public CL_VideoLocal ToClient(int userID)
        {
            CL_VideoLocal cl = new CL_VideoLocal
            {
                CRC32 = CRC32,
                DateTimeUpdated = DateTimeUpdated,
                FileName = FileName,
                FileSize = FileSize,
                Hash = Hash,
                HashSource = HashSource,
                IsIgnored = IsIgnored,
                IsVariation = IsVariation,
                Duration = Duration,
                MD5 = MD5,
                SHA1 = SHA1,
                VideoLocalID = VideoLocalID,
                Places = Places.Select(a => a.ToClient()).ToList()
            };
            VideoLocal_User userRecord = GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                cl.IsWatched = 0;
                cl.WatchedDate = null;
            }
            else
            {
                cl.IsWatched = 1;
                cl.WatchedDate = userRecord.WatchedDate;
                cl.ResumePosition = userRecord.ResumePosition;
            }

            cl.Media = GetMediaFromUser(userID);
            return cl;
        }

        // is the videolocal empty. This isn't complete, but without one or more of these the record is useless
        public bool IsEmpty()
        {
            if (!string.IsNullOrEmpty(Hash)) return false;
            if (!string.IsNullOrEmpty(MD5)) return false;
            if (!string.IsNullOrEmpty(CRC32)) return false;
            if (!string.IsNullOrEmpty(SHA1)) return false;
            if (!string.IsNullOrEmpty(FileName)) return false;
            if (FileSize > 0) return false;
            return true;
        }

        private static readonly Regex UrlSafe = new Regex("[ \\$^`:<>\\[\\]\\{\\}\"“\\+%@/;=\\?\\\\\\^\\|~‘,]",
            RegexOptions.Compiled);

        private static readonly Regex UrlSafe2 = new Regex("[^0-9a-zA-Z_\\.\\s]", RegexOptions.Compiled);

        public Media GetMediaFromUser(int userID, bool populateIfNull = false)
        {
            if (Media == null && populateIfNull)
            {
                SVR_VideoLocal_Place pl = GetBestVideoLocalPlace();
                if (pl?.FullServerPath != null)
                {
                    IFileSystem f = pl.ImportFolder.FileSystem;
                    FileSystemResult<IObject> src = f?.Resolve(pl.FullServerPath);
                    if (src != null && src.IsOk && src.Result is IFile)
                        if (pl.RefreshMediaInfo())
                            RepoFactory.VideoLocal.Save(pl.VideoLocal, true);
                }
            }
            if (Media == null) return null;
            var n = (Media) Media.Clone();
            if (n.Parts == null) return n;
            foreach (Part p in n.Parts)
            {
                string name = UrlSafe.Replace(Path.GetFileName(FileName), " ")
                    .Replace("  ", " ")
                    .Replace("  ", " ")
                    .Trim();
                name = UrlSafe2.Replace(name, string.Empty)
                    .Trim()
                    .Replace("..", ".")
                    .Replace("..", ".")
                    .Replace("__", "_")
                    .Replace("__", "_")
                    .Replace(" ", "_")
                    .Replace("_.", ".");
                while (name.StartsWith("_"))
                    name = name.Substring(1);
                while (name.StartsWith("."))
                    name = name.Substring(1);
                p.Key = ((IProvider) null).ReplaceSchemeHost(
                    ((IProvider) null).ConstructVideoLocalStream(userID, VideoLocalID, name, false));
                if (p.Streams == null) continue;
                foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == 3).ToList())
                    s.Key =
                        ((IProvider) null).ReplaceSchemeHost(
                            ((IProvider) null).ConstructFileStream(userID, s.File, false));
            }
            return n;
        }

        public CL_VideoDetailed ToClientDetailed(int userID)
        {
            CL_VideoDetailed cl = new CL_VideoDetailed();

            // get the cross ref episode
            List<CrossRef_File_Episode> xrefs = EpisodeCrossRefs;
            if (xrefs.Count == 0) return null;

            cl.Percentage = xrefs[0].Percentage;
            cl.EpisodeOrder = xrefs[0].EpisodeOrder;
            cl.CrossRefSource = xrefs[0].CrossRefSource;
            cl.AnimeEpisodeID = xrefs[0].EpisodeID;

            cl.VideoLocal_FileName = FileName;
            cl.VideoLocal_Hash = Hash;
            cl.VideoLocal_FileSize = FileSize;
            cl.VideoLocalID = VideoLocalID;
            cl.VideoLocal_IsIgnored = IsIgnored;
            cl.VideoLocal_IsVariation = IsVariation;
            cl.Places = Places.Select(a => a.ToClient()).ToList();

            cl.VideoLocal_MD5 = MD5;
            cl.VideoLocal_SHA1 = SHA1;
            cl.VideoLocal_CRC32 = CRC32;
            cl.VideoLocal_HashSource = HashSource;

            VideoLocal_User userRecord = GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                cl.VideoLocal_IsWatched = 0;
                cl.VideoLocal_WatchedDate = null;
                cl.VideoLocal_ResumePosition = 0;
            }
            else
            {
                cl.VideoLocal_IsWatched = 1;
                cl.VideoLocal_WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord != null)
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
            SVR_AniDB_File anifile = GetAniDBFile(); // to prevent multiple db calls
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
                cl.AniDB_File_IsCensored = anifile.IsCensored;
                cl.AniDB_File_IsChaptered = anifile.IsChaptered;
                cl.AniDB_File_IsDeprecated = anifile.IsDeprecated;
                cl.AniDB_File_InternalVersion = anifile.InternalVersion;

                // languages
                cl.LanguagesAudio = anifile.LanguagesRAW;
                cl.LanguagesSubtitle = anifile.SubtitlesRAW;
            }
            else
            {
                cl.AniDB_Anime_GroupName = string.Empty;
                cl.AniDB_Anime_GroupNameShort = string.Empty;
                cl.AniDB_CRC = string.Empty;
                cl.AniDB_File_AudioCodec = string.Empty;
                cl.AniDB_File_Description = string.Empty;
                cl.AniDB_File_FileExtension = string.Empty;
                cl.AniDB_File_Source = string.Empty;
                cl.AniDB_File_VideoCodec = string.Empty;
                cl.AniDB_File_VideoResolution = string.Empty;
                cl.AniDB_MD5 = string.Empty;
                cl.AniDB_SHA1 = string.Empty;
                cl.AniDB_File_FileVersion = 1;

                // languages
                cl.LanguagesAudio = string.Empty;
                cl.LanguagesSubtitle = string.Empty;
            }


            AniDB_ReleaseGroup relGroup = ReleaseGroup; // to prevent multiple db calls
            cl.ReleaseGroup = relGroup;
            cl.Media = GetMediaFromUser(userID);
            return cl;
        }

        public CL_VideoLocal_ManualLink ToContractManualLink(int userID)
        {
            CL_VideoLocal_ManualLink cl = new CL_VideoLocal_ManualLink
            {
                CRC32 = CRC32,
                DateTimeUpdated = DateTimeUpdated,
                FileName = FileName,
                FileSize = FileSize,
                Hash = Hash,
                HashSource = HashSource,
                IsIgnored = IsIgnored,
                IsVariation = IsVariation,
                MD5 = MD5,
                SHA1 = SHA1,
                VideoLocalID = VideoLocalID,
                Places = Places.Select(a => a.ToClient()).ToList()
            };
            VideoLocal_User userRecord = GetUserRecord(userID);
            if (userRecord?.WatchedDate == null)
            {
                cl.IsWatched = 0;
                cl.WatchedDate = null;
                cl.ResumePosition = 0;
            }
            else
            {
                cl.IsWatched = 1;
                cl.WatchedDate = userRecord.WatchedDate;
            }
            if (userRecord != null)
                cl.ResumePosition = userRecord.ResumePosition;
            return cl;
        }

        public bool MergeInfoFrom(VideoLocal vl)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(Hash) && !string.IsNullOrEmpty(vl.Hash))
            {
                Hash = vl.Hash;
                changed = true;
            }
            if (string.IsNullOrEmpty(CRC32) && !string.IsNullOrEmpty(vl.CRC32))
            {
                CRC32 = vl.CRC32;
                changed = true;
            }
            if (string.IsNullOrEmpty(MD5) && !string.IsNullOrEmpty(vl.MD5))
            {
                MD5 = vl.MD5;
                changed = true;
            }
            if (string.IsNullOrEmpty(SHA1) && !string.IsNullOrEmpty(vl.SHA1))
            {
                SHA1 = vl.SHA1;
                changed = true;
            }
            return changed;
        }
    }

    // This is a comparer used to sort the completeness of a videolocal, more complete first.
    // Because this is only used for comparing completeness of hashes, it does NOT follow the strict equality rules
    public class VideoLocalComparer : IComparer<VideoLocal>
    {
        public int Compare(VideoLocal x, VideoLocal y)
        {
            if (x == null) return 1;
            if (y == null) return -1;
            if (string.IsNullOrEmpty(x.Hash)) return 1;
            if (string.IsNullOrEmpty(y.Hash)) return -1;
            if (string.IsNullOrEmpty(x.CRC32)) return 1;
            if (string.IsNullOrEmpty(y.CRC32)) return -1;
            if (string.IsNullOrEmpty(x.MD5)) return 1;
            if (string.IsNullOrEmpty(y.MD5)) return -1;
            if (string.IsNullOrEmpty(x.SHA1)) return 1;
            if (string.IsNullOrEmpty(y.SHA1)) return -1;
            return x.HashSource.CompareTo(y.HashSource);
        }
    }
}
