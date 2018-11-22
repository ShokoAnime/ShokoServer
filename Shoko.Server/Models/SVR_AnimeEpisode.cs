using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_AnimeEpisode : AnimeEpisode
    {
        private DateTime _lastPlexRegen = DateTime.MinValue;
        private Video _plexContract;

        [NotMapped]
        public virtual Video PlexContract
        {
            get
            {
                if (_plexContract == null || _lastPlexRegen.Add(TimeSpan.FromMinutes(10)) > DateTime.Now)
                {
                    _lastPlexRegen = DateTime.Now;
                    return _plexContract = Helper.GenerateVideoFromAnimeEpisode(this);
                }
                return _plexContract;
            }
            set
            {
                _plexContract = value;
                _lastPlexRegen = DateTime.Now;
            }
        }

        public void CollectContractMemory()
        {
            _plexContract = null;
        }


        [NotMapped]
        public EpisodeType EpisodeTypeEnum => (EpisodeType)AniDB_Episode.EpisodeType;

        public AniDB_Episode AniDB_Episode => Repo.Instance.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

        public SVR_AnimeEpisode_User GetUserRecord(int userID)
        {
            return Repo.Instance.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, AnimeEpisodeID);
        }


        /// <summary>
        /// Gets the AnimeSeries this episode belongs to
        /// </summary>
        public SVR_AnimeSeries GetAnimeSeries()
        {
            return Repo.Instance.AnimeSeries.GetByID(AnimeSeriesID);
        }

        public List<SVR_VideoLocal> GetVideoLocals()
        {
            return Repo.Instance.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);
        }

        [NotMapped]
        public List<CrossRef_File_Episode> FileCrossRefs => Repo.Instance.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

        [NotMapped]
        public TvDB_Episode TvDBEpisode
        {
            get
            {
                return Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIDAndType(AniDB_Episode.AnimeID,CrossRefType.TvDB).
                    Select(a=>a.GetFromAniDBEpisode(AniDB_EpisodeID)).Where(a=>a!=null).Select(a => Repo.Instance.TvDB_Episode.GetByTvDBID(int.Parse(a.ProviderEpisodeID))).Where(a => a != null)
                    .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault();
            }
        }

        [NotMapped]
        public double UserRating
        {
            get
            {
                AniDB_Vote vote = Repo.Instance.AniDB_Vote.GetByEntityAndType(AnimeEpisodeID, AniDBVoteType.Episode);
                if (vote != null) return vote.VoteValue / 100D;
                return -1;
            }
        }

        public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            SVR_AnimeEpisode_User epUserRecord = GetUserRecord(userID);

            if (watched)
            {
                using (var upd = Repo.Instance.AnimeEpisode_User.BeginAddOrUpdate(() => GetUserRecord(userID)))
                {
                    // lets check if an update is actually required
                    if (upd.Entity?.WatchedDate != null && watchedDate != null &&
                        upd.Entity.WatchedDate.Equals(watchedDate.Value) ||
                        (upd.Entity?.WatchedDate == null && watchedDate == null))
                        return;

                    upd.Entity.AnimeEpisodeID = AnimeEpisodeID;
                    upd.Entity.AnimeSeriesID = AnimeSeriesID;
                    upd.Entity.JMMUserID = userID;
                    upd.Entity.WatchedCount++;

                    if (watchedDate.HasValue)
                        if (updateWatchedDate)
                            upd.Entity.WatchedDate = watchedDate.Value;

                    if (!upd.Entity.WatchedDate.HasValue) upd.Entity.WatchedDate = DateTime.Now;

                    // lets check if an update is actually required
                    if (upd.Entity?.WatchedDate != null && watchedDate != null &&
                        upd.Entity.WatchedDate.Equals(watchedDate.Value) ||
                        (upd.Entity?.WatchedDate == null && watchedDate == null))
                        return;

                    upd.Entity.AnimeEpisodeID = AnimeEpisodeID;
                    upd.Entity.AnimeSeriesID = AnimeSeriesID;
                    upd.Entity.JMMUserID = userID;
                    upd.Entity.WatchedCount++;

                    if (watchedDate.HasValue)
                        if (updateWatchedDate)
                            upd.Entity.WatchedDate = watchedDate.Value;

                    if (!upd.Entity.WatchedDate.HasValue) upd.Entity.WatchedDate = DateTime.Now;
                    epUserRecord = upd.Commit();
                }
            }
            else
            {
                if (epUserRecord != null)
                    Repo.Instance.AnimeEpisode_User.FindAndDelete(()=>Repo.Instance.AnimeEpisode_User.GetByID(epUserRecord.AnimeEpisode_UserID));
            }
        }


        public List<CL_VideoDetailed> GetVideoDetailedContracts(int userID)
        {
            // get all the cross refs
            return FileCrossRefs.Select(xref => Repo.Instance.VideoLocal.GetByHash(xref.Hash))
                .Where(v => v != null)
                .Select(v => v.ToClientDetailed(userID)).ToList();
        }

        public CL_AnimeEpisode_User GetUserContract(int userid)
        {
            SVR_AnimeEpisode_User rr = GetUserRecord(userid);
            if (rr != null)
                return rr.Contract;

            using (var upd = Repo.Instance.AnimeEpisode_User.BeginAddOrUpdate(() => GetUserRecord(userid)))
            {
                upd.Entity.PlayedCount = 0;
                upd.Entity.StoppedCount = 0;
                upd.Entity.WatchedCount = 0;
                upd.Entity.AnimeEpisodeID = AnimeEpisodeID;
                upd.Entity.AnimeSeriesID = AnimeSeriesID;
                upd.Entity.JMMUserID = userid;
                upd.Entity.WatchedDate = GetVideoLocals().Select(vid => vid.GetUserRecord(userid))
                    .FirstOrDefault(vid => vid?.WatchedDate != null)?.WatchedDate;

                rr = upd.Commit();
            }

            return rr.Contract;
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID,
            bool syncTrakt)
        {
            ToggleWatchedStatus(watched, updateOnline, watchedDate, true, userID, syncTrakt);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
            int userID, bool syncTrakt)
        {
            foreach (SVR_VideoLocal vid in GetVideoLocals())
            {
                vid.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, userID,
                    syncTrakt, true);
                vid.SetResumePosition(0, userID);
            }
        }

        [NotMapped]
        public string Title
        {
            get
            {
                var languages = Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language);
                foreach (var language in languages)
                {
                    var episode_title =
                        Repo.Instance.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, language);
                    var title = episode_title.FirstOrDefault();
                    if (string.IsNullOrEmpty(title?.Title)) continue;
                    return title?.Title;
                }

                return Repo.Instance.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, "EN").FirstOrDefault()
                    ?.Title;
            }
        }
    }
}