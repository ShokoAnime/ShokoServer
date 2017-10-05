using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.LZ4;
using NHibernate;
using Shoko.Commons.Extensions;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Models
{
    public class SVR_AnimeEpisode : AnimeEpisode
    {
        public SVR_AnimeEpisode()
        {
        }

        private DateTime _lastPlexRegen = DateTime.MinValue;
        private Video _plexContract = null;

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


        public EpisodeType EpisodeTypeEnum
        {
            get { return (EpisodeType) AniDB_Episode.EpisodeType; }
        }

        public AniDB_Episode AniDB_Episode
        {
            get { return RepoFactory.AniDB_Episode.GetByEpisodeID(this.AniDB_EpisodeID); }
        }

        public SVR_AnimeEpisode_User GetUserRecord(int userID)
        {
            return RepoFactory.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, this.AnimeEpisodeID);
        }

        public SVR_AnimeEpisode_User GetUserRecord(ISession session, int userID)
        {
            return RepoFactory.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, this.AnimeEpisodeID);
        }


        /// <summary>
        /// Gets the AnimeSeries this episode belongs to
        /// </summary>
        public SVR_AnimeSeries GetAnimeSeries()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAnimeSeries(session.Wrap());
            }
        }

        public SVR_AnimeSeries GetAnimeSeries(ISessionWrapper session)
        {
            return RepoFactory.AnimeSeries.GetByID(this.AnimeSeriesID);
        }

        public List<SVR_VideoLocal> GetVideoLocals()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetVideoLocals(session);
            }
        }

        public List<SVR_VideoLocal> GetVideoLocals(ISession session)
        {
            return RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);
        }

        public List<CrossRef_File_Episode> FileCrossRefs
        {
            get { return RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID); }
        }

        private TvDB_Episode tvDbEpisode;
        public TvDB_Episode TvDBEpisode
        {
            get
            {
                if (tvDbEpisode != null) return tvDbEpisode;
                AniDB_Episode aep = AniDB_Episode;
                List<CrossRef_AniDB_TvDBV2> xref_tvdb =
                    RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeIDEpTypeEpNumber(aep.AnimeID, aep.EpisodeType,
                        aep.EpisodeNumber);
                if (xref_tvdb.Count <= 0) xref_tvdb = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(aep.AnimeID);
                if (xref_tvdb.Count <= 0) return null;
                CrossRef_AniDB_TvDBV2 xref_tvdb2 = xref_tvdb[0];

                TvDB_Episode tvep;
                DateTime? airdate = aep.GetAirDateAsDate();
                if (airdate != null)
                {
                    foreach (var xref in xref_tvdb)
                    {
                        tvep = RepoFactory.TvDB_Episode.GetBySeriesIDAndDate(xref.TvDBID, airdate.Value);
                        if (tvep != null) return tvDbEpisode = tvep;
                    }
                }

                int epnumber = (aep.EpisodeNumber + xref_tvdb2.TvDBStartEpisodeNumber - 1) -
                               (xref_tvdb2.AniDBStartEpisodeNumber - 1);
                int season = xref_tvdb2.TvDBSeasonNumber;
                tvep =
                    RepoFactory.TvDB_Episode.GetBySeriesIDSeasonNumberAndEpisode(xref_tvdb2.TvDBID, season,
                        epnumber);
                if (tvep != null) return tvDbEpisode = tvep;

                int lastSeason = RepoFactory.TvDB_Episode.getLastSeasonForSeries(xref_tvdb2.TvDBID);
                int previousSeasonsCount = 0;
                // we checked once, so increment the season
                season++;
                previousSeasonsCount +=
                    RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(xref_tvdb2.TvDBID, season);
                do
                {
                    if (season == 0) break; // Specials will often be wrong
                    if (season > lastSeason) break;
                    if (epnumber - previousSeasonsCount <= 0) break;
                    // This should be 1 or 0, hopefully 1
                    tvep = RepoFactory.TvDB_Episode.GetBySeriesIDSeasonNumberAndEpisode(xref_tvdb2.TvDBID, season,
                        epnumber - previousSeasonsCount);

                    if (tvep != null)
                        break;
                    previousSeasonsCount +=
                        RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(xref_tvdb2.TvDBID, season);
                    season++;
                } while (true);
                return tvDbEpisode = tvep;
            }
        }

        public double UserRating
        {
            get
            {
                AniDB_Vote vote = RepoFactory.AniDB_Vote.GetByEntityAndType(AnimeEpisodeID, AniDBVoteType.Episode);
                if (vote != null) return vote.VoteValue / 100D;
                return -1;
            }
        }

        public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            SVR_AnimeEpisode_User epUserRecord = this.GetUserRecord(userID);

            if (watched)
            {
                // lets check if an update is actually required
                if (epUserRecord != null)
                {
                    if (epUserRecord.WatchedDate.HasValue && watchedDate.HasValue &&
                        epUserRecord.WatchedDate.Value.Equals(watchedDate.Value))
                    {
                        // this will happen when we are adding a new file for an episode where we already had another file
                        // and the file/episode was watched already
                        return;
                    }
                }

                if (epUserRecord == null)
                {
                    epUserRecord = new SVR_AnimeEpisode_User
                    {
                        PlayedCount = 0,
                        StoppedCount = 0,
                        WatchedCount = 0
                    };
                }
                epUserRecord.AnimeEpisodeID = this.AnimeEpisodeID;
                epUserRecord.AnimeSeriesID = this.AnimeSeriesID;
                epUserRecord.JMMUserID = userID;
                epUserRecord.WatchedCount++;

                if (watchedDate.HasValue)
                {
                    if (updateWatchedDate)
                        epUserRecord.WatchedDate = watchedDate.Value;
                }

                if (!epUserRecord.WatchedDate.HasValue) epUserRecord.WatchedDate = DateTime.Now;

                RepoFactory.AnimeEpisode_User.Save(epUserRecord);
            }
            else
            {
                if (epUserRecord != null)
                    RepoFactory.AnimeEpisode_User.Delete(epUserRecord.AnimeEpisode_UserID);
            }
        }


        public List<CL_VideoDetailed> GetVideoDetailedContracts(int userID)
        {
            List<CL_VideoDetailed> contracts = new List<CL_VideoDetailed>();

            // get all the cross refs
            foreach (CrossRef_File_Episode xref in FileCrossRefs)
            {
                SVR_VideoLocal v = RepoFactory.VideoLocal.GetByHash(xref.Hash);
                if (v != null)
                    contracts.Add(v.ToClientDetailed(userID));
            }


            return contracts;
        }

        private static object _lock = new object();

        public CL_AnimeEpisode_User GetUserContract(int userid, ISessionWrapper session = null)
        {
            lock (_lock) //Make it atomic on creation
            {
                SVR_AnimeEpisode_User rr = GetUserRecord(userid);
                if (rr != null)
                    return rr.Contract;
                rr = new SVR_AnimeEpisode_User
                {
                    PlayedCount = 0,
                    StoppedCount = 0,
                    WatchedCount = 0,
                    AnimeEpisodeID = this.AnimeEpisodeID,
                    AnimeSeriesID = this.AnimeSeriesID,
                    JMMUserID = userid,
                    WatchedDate = null
                };
                if (session != null)
                {
                    RepoFactory.AnimeEpisode_User.SaveWithOpenTransaction(session, rr);
                }
                else
                {
                    RepoFactory.AnimeEpisode_User.Save(rr);
                }

                return rr.Contract;
            }
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID,
            bool syncTrakt)
        {
            ToggleWatchedStatus(watched, updateOnline, watchedDate, true, true, userID, syncTrakt);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
            bool updateStatsCache, int userID, bool syncTrakt)
        {
            foreach (SVR_VideoLocal vid in GetVideoLocals())
            {
                vid.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, updateStatsCache, userID,
                    syncTrakt, true);
                vid.SetResumePosition(0, userID);
            }
        }
    }
}