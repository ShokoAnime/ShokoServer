using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class Trakt_ShowRepository
	{
		public void Save(Trakt_Show obj)
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

		public Trakt_Show GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<Trakt_Show>(id);
			}
		}

		public Trakt_Show GetByShowID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				Trakt_Show cr = session
					.CreateCriteria(typeof(Trakt_Show))
					.Add(Restrictions.Eq("Trakt_ShowID", id))
					.UniqueResult<Trakt_Show>();
				return cr;
			}
		}

		public Trakt_Show GetByTraktID(string id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByTraktID(session, id);
			}
		}

		public Trakt_Show GetByTraktID(ISession session, string id)
		{
			Trakt_Show cr = session
				.CreateCriteria(typeof(Trakt_Show))
				.Add(Restrictions.Eq("TraktID", id))
				.UniqueResult<Trakt_Show>();
			return cr;
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					Trakt_Show cr = GetByID(id);
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
