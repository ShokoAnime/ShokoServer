using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
	public class AnimeSeries_UserRepository
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public void Save(AnimeSeries_User obj)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}

			//logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Save: {0}", obj.AnimeSeriesID);
			//StatsCache.Instance.UpdateUsingSeries(obj.AnimeSeriesID);
		}

		public AnimeSeries_User GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AnimeSeries_User>(id);
			}
		}

		public AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AnimeSeries_User cr = session
					.CreateCriteria(typeof(AnimeSeries_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
					.UniqueResult<AnimeSeries_User>();
				return cr;
			}
		}

		public List<AnimeSeries_User> GetByUserID(int userid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var sers = session
					.CreateCriteria(typeof(AnimeSeries_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.List<AnimeSeries_User>();

				return new List<AnimeSeries_User>(sers);
			}
		}

		public List<AnimeSeries_User> GetBySeriesID(int seriesid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var sers = session
					.CreateCriteria(typeof(AnimeSeries_User))
					.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
					.List<AnimeSeries_User>();

				return new List<AnimeSeries_User>(sers);
			}
		}

		public List<AnimeSeries_User> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var grps = session
					.CreateCriteria(typeof(AnimeSeries_User))
					.List<AnimeSeries_User>();

				return new List<AnimeSeries_User>(grps);
			}
		}

		public List<AnimeSeries_User> GetMostRecentlyWatched(int userID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var series = session
					.CreateCriteria(typeof(AnimeSeries_User))
					.Add(Restrictions.Eq("JMMUserID", userID))
					.Add(Restrictions.Gt("UnwatchedEpisodeCount", 0))
					.AddOrder(Order.Desc("WatchedDate"))
					.List<AnimeSeries_User>();

				return new List<AnimeSeries_User>(series);
			}
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
