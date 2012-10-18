using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernateTest;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class AniDB_AnimeRepository
	{
		public void Save(AniDB_Anime obj)
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

		public AniDB_Anime GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Anime>(id);
			}
		}

		public AniDB_Anime GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Anime cr = session
					.CreateCriteria(typeof(AniDB_Anime))
					.Add(Restrictions.Eq("AnimeID", id))
					.UniqueResult<AniDB_Anime>();
				return cr;
			}
		}

		public AniDB_Anime GetByAnimeID(ISession session, int id)
		{
			AniDB_Anime cr = session
				.CreateCriteria(typeof(AniDB_Anime))
				.Add(Restrictions.Eq("AnimeID", id))
				.UniqueResult<AniDB_Anime>();
			return cr;
		}

		public List<AniDB_Anime> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Anime))
					.List<AniDB_Anime>();

				return new List<AniDB_Anime>(objs);
			}
		}

		public List<AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Anime))
					.Add(Restrictions.Ge("AirDate", startDate))
					.Add(Restrictions.Le("AirDate", endDate))
					.AddOrder(Order.Asc("AirDate"))
					.List<AniDB_Anime>();

				return new List<AniDB_Anime>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Anime cr = GetByID(id);
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
