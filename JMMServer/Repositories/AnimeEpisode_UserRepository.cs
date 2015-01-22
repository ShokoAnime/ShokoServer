using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using BinaryNorthwest;
using NHibernate;


namespace JMMServer.Repositories
{
	public class AnimeEpisode_UserRepository
	{
		public void Save(AnimeEpisode_User obj)
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
		}

		public AnimeEpisode_User GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AnimeEpisode_User>(id);
			}
		}

		public List<AnimeEpisode_User> GetBySeriesID(int seriesid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(AnimeEpisode_User))
					.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
					.List<AnimeEpisode_User>();

				return new List<AnimeEpisode_User>(eps);
			}
		}

		public AnimeEpisode_User GetByUserIDAndEpisodeID(int userid, int epid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByUserIDAndEpisodeID(session, userid, epid);
			}
		}

		public AnimeEpisode_User GetByUserIDAndEpisodeID(ISession session, int userid, int epid)
		{
			AnimeEpisode_User obj = session
				.CreateCriteria(typeof(AnimeEpisode_User))
				.Add(Restrictions.Eq("JMMUserID", userid))
				.Add(Restrictions.Eq("AnimeEpisodeID", epid))
				.UniqueResult<AnimeEpisode_User>();

			return obj;
		}

		public List<AnimeEpisode_User> GetByUserID(int userid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(AnimeEpisode_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.List<AnimeEpisode_User>();

				return new List<AnimeEpisode_User>(eps);
			}
		}

		public List<AnimeEpisode_User> GetMostRecentlyWatched(int userID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetMostRecentlyWatched(session, userID);
			}
		}

		public List<AnimeEpisode_User> GetMostRecentlyWatched(ISession session, int userID)
		{
			var eps = session
				.CreateCriteria(typeof(AnimeEpisode_User))
				.Add(Restrictions.Eq("JMMUserID", userID))
				.Add(Restrictions.Gt("WatchedCount", 0))
				.AddOrder(Order.Desc("WatchedDate"))
				.SetMaxResults(100)
				.List<AnimeEpisode_User>();

			return new List<AnimeEpisode_User>(eps);
		}

        public List<AnimeEpisode_User> GetLastWatchedEpisode()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AnimeEpisode_User))
                    .Add(Restrictions.Gt("WatchedCount", 0))
                    .AddOrder(Order.Desc("WatchedDate"))
                    .SetMaxResults(1)
                    .List<AnimeEpisode_User>();

                return new List<AnimeEpisode_User>(eps);
            }
        }

		public List<AnimeEpisode_User> GetByEpisodeID(int epid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(AnimeEpisode_User))
					.Add(Restrictions.Eq("AnimeEpisodeID", epid))
					.List<AnimeEpisode_User>();

				return new List<AnimeEpisode_User>(eps);
			}
		}

		public List<AnimeEpisode_User> GetByUserIDAndSeriesID(int userid, int seriesid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByUserIDAndSeriesID(session, userid, seriesid);
			}
		}

		public List<AnimeEpisode_User> GetByUserIDAndSeriesID(ISession session, int userid, int seriesid)
		{
			var eps = session
				.CreateCriteria(typeof(AnimeEpisode_User))
				.Add(Restrictions.Eq("JMMUserID", userid))
				.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
				.List<AnimeEpisode_User>();

			return new List<AnimeEpisode_User>(eps);
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AnimeEpisode_User cr = GetByID(id);
					if (cr != null)
					{
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}
		}
	}
}
