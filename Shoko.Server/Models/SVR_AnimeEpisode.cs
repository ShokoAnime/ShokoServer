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


        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

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


        public EpisodeType EpisodeTypeEnum => (EpisodeType) AniDB_Episode.EpisodeType;

        public AniDB_Episode AniDB_Episode => Repo.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

        public SVR_AnimeEpisode_User GetUserRecord(int userID) => Repo.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, AnimeEpisodeID);

        /// <summary>
        /// Gets the AnimeSeries this episode belongs to
        /// </summary>
        public SVR_AnimeSeries GetAnimeSeries() => Repo.AnimeSeries.GetByID(AnimeSeriesID);

        public List<SVR_VideoLocal> GetVideoLocals() => Repo.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

        public List<CrossRef_File_Episode> FileCrossRefs => Repo.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

        private TvDB_Episode tvDbEpisode;
        [NotMapped]
        public TvDB_Episode TvDBEpisode
        {
            get
            {
                if (tvDbEpisode != null) return tvDbEpisode;
                AniDB_Episode aep = AniDB_Episode;

                List<CrossRef_AniDB_TvDBV2> xref_tvdb = Repo.CrossRef_AniDB_TvDBV2.GetByAnimeIDEpTypeEpNumber(aep.AnimeID, aep.EpisodeType,
                        aep.EpisodeNumber);
                TvDB_Episode tvep;

                if (aep.EpisodeType == (int) EpisodeType.Episode && xref_tvdb.Count <= 0)
                    xref_tvdb = Repo.CrossRef_AniDB_TvDBV2.GetByAnimeID(aep.AnimeID);
                if (xref_tvdb.Count <= 0) return null;
                CrossRef_AniDB_TvDBV2 xref_tvdb2 = xref_tvdb[0];

                DateTime? airdate = aep.GetAirDateAsDate();
                if (aep.EpisodeType == (int) EpisodeType.Episode && airdate != null)
                    foreach (var xref in xref_tvdb)
                    {
                        tvep = Repo.TvDB_Episode.GetBySeriesIDAndDate(xref.TvDBID, airdate.Value);
                        if (tvep != null) return tvDbEpisode = tvep;
                    }

                int epnumber = (aep.EpisodeNumber + xref_tvdb2.TvDBStartEpisodeNumber - 1) -
                               (xref_tvdb2.AniDBStartEpisodeNumber - 1);
                int season = xref_tvdb2.TvDBSeasonNumber;
                tvep = Repo.TvDB_Episode.GetBySeriesIDSeasonNumberAndEpisode(xref_tvdb2.TvDBID, season, epnumber);
                if (tvep != null) return tvDbEpisode = tvep;

                int lastSeason = Repo.TvDB_Episode.GetLastSeasonForSeries(xref_tvdb2.TvDBID);
                int previousSeasonsCount = 0;
                // we checked once, so increment the season
                season++;
                previousSeasonsCount +=Repo.TvDB_Episode.GetNumberOfEpisodesForSeason(xref_tvdb2.TvDBID, season);
                do
                {
                    if (season == 0) break; // Specials will often be wrong
                    if (season > lastSeason) break;
                    if (epnumber - previousSeasonsCount <= 0) break;
                    // This should be 1 or 0, hopefully 1
                    tvep = Repo.TvDB_Episode.GetBySeriesIDSeasonNumberAndEpisode(xref_tvdb2.TvDBID, season, epnumber - previousSeasonsCount);

                    if (tvep != null)
                        break;
                    previousSeasonsCount += Repo.TvDB_Episode.GetNumberOfEpisodesForSeason(xref_tvdb2.TvDBID, season);
                    season++;
                } while (true);
                return tvDbEpisode = tvep;
            }
            set => tvDbEpisode = value;
        }
        [NotMapped]
        public double UserRating
        {
            get
            {
                AniDB_Vote vote = Repo.AniDB_Vote.GetByEntityAndType(AnimeEpisodeID, AniDBVoteType.Episode);
                if (vote != null) return vote.VoteValue / 100D;
                return -1;
            }
        }

        public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            
            SVR_AnimeEpisode_User epUserRecord = GetUserRecord(userID);

            if (watched)
            {
                // lets check if an update is actually required
                if (epUserRecord?.WatchedDate != null && watchedDate != null &&
                    epUserRecord.WatchedDate.Equals(watchedDate.Value) ||
                    (epUserRecord?.WatchedDate == null && watchedDate == null))
                    return;
                using (var upd = Repo.AnimeEpisode_User.BeginAddOrUpdate(() => epUserRecord))
                {
                    if (epUserRecord == null)
                    {
                        upd.Entity.PlayedCount = 0;
                        upd.Entity.StoppedCount = 0;
                        upd.Entity.WatchedCount = 0;
                    }

                    upd.Entity.AnimeEpisodeID = AnimeEpisodeID;
                    upd.Entity.AnimeSeriesID = AnimeSeriesID;
                    upd.Entity.JMMUserID = userID;
                    upd.Entity.WatchedCount++;

                    if (watchedDate.HasValue && updateWatchedDate)
                        upd.Entity.WatchedDate = watchedDate.Value;

                    if (!upd.Entity.WatchedDate.HasValue)
                        upd.Entity.WatchedDate = DateTime.Now;
                    upd.Commit();
                }
            }
            else if (epUserRecord != null)
                Repo.AnimeEpisode_User.Delete(epUserRecord);
        }


        public List<CL_VideoDetailed> GetVideoDetailedContracts(int userID)
        {
            // get all the cross refs
            return FileCrossRefs.Select(xref => Repo.VideoLocal.GetByHash(xref.Hash)).Where(v => v != null).Select(v => v.ToClientDetailed(userID)).ToList();
        }

        public CL_AnimeEpisode_User GetUserContract(int userid)
        {
            SVR_AnimeEpisode_User rr = GetUserRecord(userid);
            if (rr == null)
            {
                using (var upd = Repo.AnimeEpisode_User.BeginAdd())
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
            }
            return rr.Contract;
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID, bool syncTrakt)
        {
            ToggleWatchedStatus(watched, updateOnline, watchedDate, true, userID, syncTrakt);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID, bool syncTrakt)
        {
            foreach (SVR_VideoLocal vid in GetVideoLocals())
            {
                vid.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, userID, syncTrakt, true);
                vid.SetResumePosition(0, userID);
            }
        }
    }
}