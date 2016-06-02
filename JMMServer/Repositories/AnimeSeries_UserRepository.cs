using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.PlexAndKodi;
using NHibernate.Criterion;
using NLog;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
	public class AnimeSeries_UserRepository
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

	    private static PocoCache<int, AnimeSeries_User> Cache;
	    private static PocoIndex<int, AnimeSeries_User, int> Users;
	    private static PocoIndex<int, AnimeSeries_User, int> Series;
	    private static PocoIndex<int, AnimeSeries_User, int, int> UsersSeries;

        public static void InitCache()
	    {
            string t = "AnimeSeries_User";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AnimeSeries_UserRepository repo =new AnimeSeries_UserRepository();
	        Cache = new PocoCache<int, AnimeSeries_User>(repo.InternalGetAll(),a=>a.AnimeSeries_UserID);
            Users=Cache.CreateIndex(a=>a.JMMUserID);
            Series=Cache.CreateIndex(a=>a.AnimeSeriesID);
            UsersSeries=Cache.CreateIndex(a=>a.JMMUserID,a=>a.AnimeSeriesID);
            int cnt = 0;
            List<AnimeSeries_User> sers = Cache.Values.Where(a => a.PlexContractVersion < AnimeGroup_User.PLEXCONTRACT_VERSION).ToList();
            int max = sers.Count;
            foreach (AnimeSeries_User g in sers)
            {
                repo.Save(g);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " DbRegen - " + max + "/" + max);

        }

      
        private List<AnimeSeries_User> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var grps = session
                    .CreateCriteria(typeof(AnimeSeries_User))
                    .List<AnimeSeries_User>();

                return new List<AnimeSeries_User>(grps);
            }
        }

        public void Save(AnimeSeries_User obj)
		{
            UpdatePlexKodiContracts(obj);			
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}
            Cache.Update(obj);
			//logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Save: {0}", obj.AnimeSeriesID);
			//StatsCache.Instance.UpdateUsingSeries(obj.AnimeSeriesID);
		}
        private void UpdatePlexKodiContracts(AnimeSeries_User ugrp)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AnimeSeriesRepository repo = new AnimeSeriesRepository();
                AnimeSeries ser = repo.GetByID(ugrp.AnimeSeriesID);
                if (ser == null)
                    return;
                Contract_AnimeSeries con = ser.GetUserContract(ugrp.JMMUserID);
                if (con ==null)
                    return;
                ugrp.PlexContract = Helper.GenerateFromSeries(con,ser,ser.GetAnime(session),ugrp.JMMUserID);
            }
        }

        public AnimeSeries_User GetByID(int id)
        {
            return Cache.Get(id);
        }

		public AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
		{
		    return UsersSeries.GetOne(userid, seriesid);
		}

		public AnimeSeries_User GetByUserAndSeriesID(ISession session, int userid, int seriesid)
		{
		    return GetByUserAndSeriesID(userid, seriesid);
		}

		public List<AnimeSeries_User> GetByUserID(int userid)
		{
		    return Users.GetMultiple(userid);
        }

		public List<AnimeSeries_User> GetBySeriesID(int seriesid)
		{
		    return Series.GetMultiple(seriesid);
        }

        public List<AnimeSeries_User> GetAll()
		{
		    return Cache.Values.ToList();
		}

		public List<AnimeSeries_User> GetMostRecentlyWatched(int userID)
		{
		    return GetByUserID(userID).Where(a => a.UnwatchedEpisodeCount > 0).OrderByDescending(a => a.WatchedDate).ToList();
		}

		public List<AnimeSeries_User> GetMostRecentlyWatched(ISession session, int userID)
		{
		    return GetMostRecentlyWatched(userID);
		}

		public void Delete(int id)
		{
			AnimeSeries_User cr = null;
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					cr = GetByID(id);
                    if (cr != null)
					{
                        Cache.Remove(cr);
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}
			//if (cr != null)
			//{
			//	logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Delete: {0}", cr.AnimeSeriesID);
			//	StatsCache.Instance.UpdateUsingSeries(cr.AnimeSeriesID);
			//}
		}
	}
}
