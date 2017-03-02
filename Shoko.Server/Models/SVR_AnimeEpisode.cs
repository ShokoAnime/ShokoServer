using System;
using System.Collections.Generic;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.LZ4;
using NHibernate;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Models
{
    public class SVR_AnimeEpisode : AnimeEpisode
    {
        public SVR_AnimeEpisode()
        {
        }
        #region Server DB columns

        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

        #endregion
        public const int PLEXCONTRACT_VERSION = 6;


        private Video _plexcontract = null;


        public virtual Video PlexContract
        {
            get
            {
                if ((_plexcontract == null) && (PlexContractBlob != null) && (PlexContractBlob.Length > 0) &&
                    (PlexContractSize > 0))
                    _plexcontract = CompressionHelper.DeserializeObject<Video>(PlexContractBlob, PlexContractSize);
                return _plexcontract;
            }
            set
            {
                _plexcontract = value;
                int outsize;
                PlexContractBlob = CompressionHelper.SerializeObject(value, out outsize, true);
                PlexContractSize = outsize;
                PlexContractVersion = PLEXCONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _plexcontract = null;
        }


        public enEpisodeType EpisodeTypeEnum
        {
            get { return (enEpisodeType) AniDB_Episode.EpisodeType; }
        }

        public AniDB_Episode AniDB_Episode
        {
            get
            {
                return RepoFactory.AniDB_Episode.GetByEpisodeID(this.AniDB_EpisodeID);
            }
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
            get
            {
                return RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);
            }
        }

	    public TvDB_Episode TvDBEpisode
	    {
		    get
		    {
			    using (var session = DatabaseFactory.SessionFactory.OpenSession())
			    {
				    AniDB_Episode aep = AniDB_Episode;
				    List<CrossRef_AniDB_TvDBV2> xref_tvdb =
					    RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeIDEpTypeEpNumber(session, aep.AnimeID,
						    aep.EpisodeType, aep.EpisodeNumber);
				    if (xref_tvdb != null && xref_tvdb.Count > 0)
				    {
					    CrossRef_AniDB_TvDBV2 xref_tvdb2 = xref_tvdb[0];
					    int epnumber = (aep.EpisodeNumber + xref_tvdb2.TvDBStartEpisodeNumber - 1) -
					                   (xref_tvdb2.AniDBStartEpisodeNumber - 1);
					    TvDB_Episode tvep = null;
					    int season = xref_tvdb2.TvDBSeasonNumber;
					    List<TvDB_Episode> tvdb_eps = RepoFactory.TvDB_Episode.GetBySeriesIDAndSeasonNumber(xref_tvdb2.TvDBID, season);
					    tvep = tvdb_eps.Find(a => a.EpisodeNumber == epnumber);
					    if (tvep != null) return tvep;

					    int lastSeason = RepoFactory.TvDB_Episode.getLastSeasonForSeries(xref_tvdb2.TvDBID);
					    int previousSeasonsCount = 0;
					    // we checked once, so increment the season
					    season++;
					    previousSeasonsCount += tvdb_eps.Count;
					    do
					    {
						    if (season == 0) break; // Specials will often be wrong
						    if (season > lastSeason) break;
						    if (epnumber - previousSeasonsCount <= 0) break;
						    // This should be 1 or 0, hopefully 1
						    tvdb_eps = RepoFactory.TvDB_Episode.GetBySeriesIDAndSeasonNumber(xref_tvdb2.TvDBID, season);
						    tvep = tvdb_eps.Find(a => a.EpisodeNumber == epnumber - previousSeasonsCount);

						    if (tvep != null)
						    {
							    break;
						    }
						    previousSeasonsCount += tvdb_eps.Count;
						    season++;
					    } while (true);
					    return tvep;
				    }
				    return null;
			    }
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
                    epUserRecord = new SVR_AnimeEpisode_User();
                    epUserRecord.PlayedCount = 0;
                    epUserRecord.StoppedCount = 0;
                    epUserRecord.WatchedCount = 0;
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
                SVR_VideoLocal v=RepoFactory.VideoLocal.GetByHash(xref.Hash);
                if (v != null)
                    contracts.Add(v.ToClientDetailed(userID));
            }


            return contracts;
        }

        private static object _lock = new object();

        public CL_AnimeEpisode_User GetUserContract(int userid, ISessionWrapper session = null)
        {
            lock(_lock) //Make it atomic on creation
            { 
                SVR_AnimeEpisode_User rr = GetUserRecord(userid);
                if (rr != null)
                    return rr.Contract;
                rr = new SVR_AnimeEpisode_User();
                rr.PlayedCount = 0;
                rr.StoppedCount = 0;
                rr.WatchedCount = 0;
                rr.AnimeEpisodeID = this.AnimeEpisodeID;
                rr.AnimeSeriesID = this.AnimeSeriesID;
                rr.JMMUserID = userid;
                rr.WatchedDate = null;

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