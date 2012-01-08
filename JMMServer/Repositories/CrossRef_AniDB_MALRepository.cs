using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class CrossRef_AniDB_MALRepository
	{
		public void Save(CrossRef_AniDB_MAL obj)
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

		public CrossRef_AniDB_MAL GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<CrossRef_AniDB_MAL>(id);
			}
		}

		public CrossRef_AniDB_MAL GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				CrossRef_AniDB_MAL cr = session
					.CreateCriteria(typeof(CrossRef_AniDB_MAL))
					.Add(Restrictions.Eq("AnimeID", id))
					.UniqueResult<CrossRef_AniDB_MAL>();
				return cr;
			}
		}

		public CrossRef_AniDB_MAL GetByMALID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				CrossRef_AniDB_MAL cr = session
					.CreateCriteria(typeof(CrossRef_AniDB_MAL))
					.Add(Restrictions.Eq("MALID", id))
					.UniqueResult<CrossRef_AniDB_MAL>();
				return cr;
			}
		}

		public List<CrossRef_AniDB_MAL> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var series = session
					.CreateCriteria(typeof(CrossRef_AniDB_MAL))
					.List<CrossRef_AniDB_MAL>();

				return new List<CrossRef_AniDB_MAL>(series);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					CrossRef_AniDB_MAL cr = GetByID(id);
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
