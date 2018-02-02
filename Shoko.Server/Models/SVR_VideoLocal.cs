using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using Nancy.Json;
using NLog;
using NutzCode.CloudFileSystem;
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
using Shoko.Server.Repositories.Repos;
using Stream = Shoko.Models.PlexAndKodi.Stream;

namespace Shoko.Server.Models
{
    public class SVR_VideoLocal : VideoLocal, IHash
    {
        #region DB columns

        public int MediaVersion { get; set; }
        public byte[] MediaBlob { get; set; }
        public int MediaSize { get; set; }

        #endregion


        public const int MEDIA_VERSION = 3;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly Regex UrlSafe = new Regex("[ \\$^`:<>\\[\\]\\{\\}\"“\\+%@/;=\\?\\\\\\^\\|~‘,]", RegexOptions.Compiled);

        private static readonly Regex UrlSafe2 = new Regex("[^0-9a-zA-Z_\\.\\s]", RegexOptions.Compiled);


        internal Media _media;
        [NotMapped]
        public virtual Media Media
        {
            get
            {
                if (_media == null && MediaBlob != null && MediaBlob.Length > 0 && MediaSize > 0)
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


        public List<SVR_VideoLocal_Place> Places => Repo.VideoLocal_Place.GetByVideoLocal(VideoLocalID);

        [NotMapped]
        internal AniDB_ReleaseGroup ReleaseGroup
        {
            get
            {
                SVR_AniDB_File anifile = GetAniDBFile();
                if (anifile == null) return null;

                return Repo.AniDB_ReleaseGroup.GetByID(anifile.GroupID);
            }
        }

        [NotMapped]
        public List<CrossRef_File_Episode> EpisodeCrossRefs
        {
            get
            {
                if (Hash.Length == 0) return new List<CrossRef_File_Episode>();

                return Repo.CrossRef_File_Episode.GetByHash(Hash);
            }
        }

        [ScriptIgnore]
        [NotMapped]
        public string ED2KHash
        {
            get => Hash;
            set => Hash = value;
        }

        [NotMapped]
        public string Info => FileName ?? string.Empty;


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
            sb.Append("Hash: " + Hash);
            sb.Append(Environment.NewLine);
            sb.Append("FileSize: " + FileSize);
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }


        public SVR_AniDB_File GetAniDBFile() => Repo.AniDB_File.GetByHash(Hash);


        public VideoLocal_User GetUserRecord(int userID) => Repo.VideoLocal_User.GetByUserIDAndVideoLocalID(userID, VideoLocalID);

        public List<SVR_AnimeEpisode> GetAnimeEpisodes() => Repo.AnimeEpisode.GetByHash(Hash);

        private void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            VideoLocal_User vidUserRecord = GetUserRecord(userID);
            if (watched)
            {
                using (var upd = Repo.VideoLocal_User.BeginAddOrUpdate(() => GetUserRecord(userID)))
                {
                    upd.Entity.WatchedDate = DateTime.Now;
                    upd.Entity.JMMUserID = userID;
                    upd.Entity.VideoLocalID = VideoLocalID;
                    if (watchedDate.HasValue && updateWatchedDate)
                        upd.Entity.WatchedDate = watchedDate.Value;
                    upd.Commit();
                }
            }
            else if (vidUserRecord != null)
                Repo.VideoLocal_User.Delete(vidUserRecord);
        }

        public static IFile ResolveFile(string fullname)
        {
            if (string.IsNullOrEmpty(fullname)) return null;
            (SVR_ImportFolder, string) tup = VideoLocal_PlaceRepository.GetFromFullPath(fullname);
            IFileSystem fs = tup.Item1?.FileSystem;
            if (fs == null)
                return null;
            try
            {
                IObject fobj = fs.Resolve(fullname);
                if (fobj.Status != Status.Ok || fobj is IDirectory) return null;
                return fobj as IFile;
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
        [NotMapped]
        public string FileName => GetBestVideoLocalPlace().FileName;
        
        public SVR_VideoLocal_Place GetBestVideoLocalPlace() => Places.Where(p => !string.IsNullOrEmpty(p?.FullServerPath)).OrderBy(a => a.ImportFolderType).FirstOrDefault(p => ResolveFile(p.FullServerPath) != null);

        public void SetResumePosition(long resumeposition, int userID)
        {
            using (var upd = Repo.VideoLocal_User.BeginAddOrUpdate(() => GetUserRecord(userID)))
            {
                upd.Entity.JMMUserID = userID;
                upd.Entity.VideoLocalID = VideoLocalID;
                upd.Entity.ResumePosition = resumeposition;
                upd.Commit();
            }
        }

        public void ToggleWatchedStatus(bool watched, int userID)
        {
            ToggleWatchedStatus(watched, true, null, true, userID, true, true);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID, bool syncTrakt, bool updateWatchedDate)
        {
            SVR_JMMUser user = Repo.JMMUser.GetByID(userID);
            if (user == null) return;

            List<SVR_JMMUser> aniDBUsers = Repo.JMMUser.GetAniDBUsers();

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
                using (var anupd = Repo.AniDB_File.BeginAddOrUpdate(() => Repo.AniDB_File.GetByHash(Hash)))
                {
                    if (anupd.IsUpdate)
                    {
                        anupd.Entity.IsWatched = mywatched;
                        if (watched)
                            anupd.Entity.WatchedDate = watchedDate ?? DateTime.Now;
                        else
                            anupd.Entity.WatchedDate = null;
                        anupd.Commit();
                    }
                }
                if (updateOnline)
                    if (watched && ServerSettings.AniDB_MyList_SetWatched || !watched && ServerSettings.AniDB_MyList_SetUnwatched)
                    {
                        CommandRequest_UpdateMyListFileStatus cmd = new CommandRequest_UpdateMyListFileStatus(Hash, watched, false, watchedDate.HasValue ? AniDB.GetAniDBDateAsSeconds(watchedDate) : 0);
                        cmd.Save();
                    }
            }

            // now find all the episode records associated with this video file
            // but we also need to check if theer are any other files attached to this episode with a watched
            // status, 


            SVR_AnimeSeries ser = null;
            // get all files associated with this episode
            List<CrossRef_File_Episode> xrefs = Repo.CrossRef_File_Episode.GetByHash(Hash);
            Dictionary<int, SVR_AnimeSeries> toUpdateSeries = new Dictionary<int, SVR_AnimeSeries>();
            if (watched)
            {
                // find the total watched percentage
                // eg one file can have a % = 100
                // or if 2 files make up one episodes they will each have a % = 50

                foreach (CrossRef_File_Episode xref in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    foreach (SVR_AnimeEpisode ep in Repo.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID))
                    {
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
                            if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                                toUpdateSeries.Add(ser.AnimeSeriesID, ser);
                            if (user.IsAniDBUser == 0)
                                ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
                            else
                                foreach (SVR_JMMUser juser in aniDBUsers)
                                    if (juser.IsAniDBUser == 1)
                                        ep.SaveWatchedStatus(true, juser.JMMUserID, watchedDate, updateWatchedDate);

                            if (syncTrakt && ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                            {
                                CommandRequest_TraktHistoryEpisode cmdSyncTrakt = new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Add);
                                cmdSyncTrakt.Save();
                            }

                            if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) && !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                            {
                                CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                                cmdMAL.Save();
                            }
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
                    foreach (SVR_AnimeEpisode ep in Repo.AnimeEpisode.GetByAniDBEpisodeID(xrefEp.EpisodeID))
                    {
                        ser = ep.GetAnimeSeries();
                        if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                            toUpdateSeries.Add(ser.AnimeSeriesID, ser);
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

                            if (syncTrakt && ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                            {
                                CommandRequest_TraktHistoryEpisode cmdSyncTrakt = new CommandRequest_TraktHistoryEpisode(ep.AnimeEpisodeID, TraktSyncAction.Remove);
                                cmdSyncTrakt.Save();
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) && !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                    cmdMAL.Save();
                }
            }


            // update stats for groups and series
            if (toUpdateSeries.Count > 0 && updateStats)
                foreach (SVR_AnimeSeries s in toUpdateSeries.Values)
                    // update all the groups above this series in the heirarchy
                    SVR_AnimeSeries.UpdateStats(s, true, true, true);
        }

        public override string ToString()
        {
            return $"{Hash}";
        }


        public static (SVR_VideoLocal Videolocal, CL_VideoLocal ClientVideoLocal) ToClient(SVR_VideoLocal videoLocal, int userID)
        {
            CL_VideoLocal cl = new CL_VideoLocal
            {
                CRC32 = videoLocal.CRC32,
                DateTimeUpdated = videoLocal.DateTimeUpdated,
                FileSize = videoLocal.FileSize,
                Hash = videoLocal.Hash,
                HashSource = videoLocal.HashSource,
                IsIgnored = videoLocal.IsIgnored,
                IsVariation = videoLocal.IsVariation,
                Duration = videoLocal.Duration,
                MD5 = videoLocal.MD5,
                SHA1 = videoLocal.SHA1,
                VideoLocalID = videoLocal.VideoLocalID,
                Places = videoLocal.Places.Select(a => a.ToClient()).ToList()
            };
            VideoLocal_User userRecord = videoLocal.GetUserRecord(userID);
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
            (videoLocal,cl.Media)=GetMediaFromUser(videoLocal, userID);
            return (videoLocal, cl);
        }

        // is the videolocal empty. This isn't complete, but without one or more of these the record is useless
        public bool IsEmpty()
        {
            if (!string.IsNullOrEmpty(Hash)) return false;
            if (!string.IsNullOrEmpty(MD5)) return false;
            if (!string.IsNullOrEmpty(CRC32)) return false;
            if (!string.IsNullOrEmpty(SHA1)) return false;
            if (FileSize > 0) return false;
            return true;
        }

        public static (SVR_VideoLocal, Media) GetMediaFromUser(SVR_VideoLocal videoLocal, int userID)
        {
            Media n;
            string fileName = string.Empty;
            if (videoLocal.Media == null)
            {
                SVR_VideoLocal_Place pl = videoLocal.GetBestVideoLocalPlace();
                if (pl?.FullServerPath != null)
                {
                    IFileSystem f = pl.ImportFolder.FileSystem;
                    IObject src = f?.Resolve(pl.FullServerPath);
                    if (src!=null && src.Status == Status.Ok && src is IFile)
                    {

                        using (var v = Repo.VideoLocal.BeginAddOrUpdate(()=>Repo.VideoLocal.GetByID(videoLocal.VideoLocalID)))
                        {
                            if (pl.RefreshMediaInfo(v.Entity))
                                videoLocal=v.Commit();
                        }
                    }

                    fileName = Path.GetFileName(pl.FullServerPath);
                }
            }

            if (videoLocal.Media == null) return (videoLocal, null);
            n = videoLocal.Media.DeepClone();
            if (n?.Parts == null) return (videoLocal,n);
            foreach (Part p in n.Parts)
            {
                string name = UrlSafe.Replace(Path.GetFileName(fileName), " ").Replace("  ", " ").Replace("  ", " ").Trim();
                name = UrlSafe2.Replace(name, string.Empty).Trim().Replace("..", ".").Replace("..", ".").Replace("__", "_").Replace("__", "_").Replace(" ", "_").Replace("_.", ".");
                while (name.StartsWith("_"))
                    name = name.Substring(1);
                while (name.StartsWith("."))
                    name = name.Substring(1);
                p.Key = ((IProvider) null).ReplaceSchemeHost(((IProvider) null).ConstructVideoLocalStream(userID, videoLocal.VideoLocalID.ToString(), name, false));
                if (p.Streams == null) continue;
                foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == "3").ToList())
                    s.Key = ((IProvider) null).ReplaceSchemeHost(((IProvider) null).ConstructFileStream(userID, s.File, false));
            }

            return (videoLocal, n);
        }

        public static (SVR_VideoLocal, CL_VideoDetailed) ToClientDetailed(SVR_VideoLocal videoLocal, int userID)
        {
            CL_VideoDetailed cl = new CL_VideoDetailed();

            // get the cross ref episode
            List<CrossRef_File_Episode> xrefs = videoLocal.EpisodeCrossRefs;
            if (xrefs.Count == 0) return (videoLocal, null);

            cl.Percentage = xrefs[0].Percentage;
            cl.EpisodeOrder = xrefs[0].EpisodeOrder;
            cl.CrossRefSource = xrefs[0].CrossRefSource;
            cl.AnimeEpisodeID = xrefs[0].EpisodeID;

            cl.VideoLocal_Hash = videoLocal.Hash;
            cl.VideoLocal_FileSize = videoLocal.FileSize;
            cl.VideoLocalID = videoLocal.VideoLocalID;
            cl.VideoLocal_IsIgnored = videoLocal.IsIgnored;
            cl.VideoLocal_IsVariation = videoLocal.IsVariation;
            cl.Places = videoLocal.Places.Select(a => a.ToClient()).ToList();

            cl.VideoLocal_MD5 = videoLocal.MD5;
            cl.VideoLocal_SHA1 = videoLocal.SHA1;
            cl.VideoLocal_CRC32 = videoLocal.CRC32;
            cl.VideoLocal_HashSource = videoLocal.HashSource;

            VideoLocal_User userRecord = videoLocal.GetUserRecord(userID);
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
            cl.VideoInfo_AudioBitrate = videoLocal.AudioBitrate;
            cl.VideoInfo_AudioCodec = videoLocal.AudioCodec;
            cl.VideoInfo_Duration = videoLocal.Duration;
            cl.VideoInfo_VideoBitrate = videoLocal.VideoBitrate;
            cl.VideoInfo_VideoBitDepth = videoLocal.VideoBitDepth;
            cl.VideoInfo_VideoCodec = videoLocal.VideoCodec;
            cl.VideoInfo_VideoFrameRate = videoLocal.VideoFrameRate;
            cl.VideoInfo_VideoResolution = videoLocal.VideoResolution;

            // AniDB File
            SVR_AniDB_File anifile = videoLocal.GetAniDBFile(); // to prevent multiple db calls
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


            AniDB_ReleaseGroup relGroup = videoLocal.ReleaseGroup; // to prevent multiple db calls
            cl.ReleaseGroup = relGroup;
            (videoLocal, cl.Media) = GetMediaFromUser(videoLocal, userID);
            return (videoLocal, cl);
        }

        public CL_VideoLocal_ManualLink ToContractManualLink(int userID)
        {
            CL_VideoLocal_ManualLink cl = new CL_VideoLocal_ManualLink
            {
                CRC32 = CRC32,
                DateTimeUpdated = DateTimeUpdated,
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

        public bool MergeInfoFrom_RA(VideoLocal vl)
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
        public bool ForceMergeInfoFrom_RA(VideoLocal vl)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(vl.Hash))
            {
                Hash = vl.Hash;
                changed = true;
            }

            if (!string.IsNullOrEmpty(vl.CRC32))
            {
                CRC32 = vl.CRC32;
                changed = true;
            }

            if (!string.IsNullOrEmpty(vl.MD5))
            {
                MD5 = vl.MD5;
                changed = true;
            }

            if (!string.IsNullOrEmpty(vl.SHA1))
            {
                SHA1 = vl.SHA1;
                changed = true;
            }

            return changed;
        }
        public bool MergeInfoTo(VideoLocal vl_ra)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(vl_ra.Hash) && !string.IsNullOrEmpty(Hash))
            {
                vl_ra.Hash = Hash;
                changed = true;
            }

            if (string.IsNullOrEmpty(vl_ra.CRC32) && !string.IsNullOrEmpty(CRC32))
            {
                vl_ra.CRC32 = CRC32;
                changed = true;
            }

            if (string.IsNullOrEmpty(vl_ra.MD5) && !string.IsNullOrEmpty(MD5))
            {
                vl_ra.MD5 = MD5;
                changed = true;
            }

            if (string.IsNullOrEmpty(vl_ra.SHA1) && !string.IsNullOrEmpty(SHA1))
            {
                vl_ra.SHA1 = SHA1;
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