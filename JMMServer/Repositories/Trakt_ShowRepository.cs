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

		public Trakt_Show GetByTraktSlug(string slug)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByTraktSlug(session, slug);
			}
		}

		public Trakt_Show GetByTraktSlug(ISession session, string slug)
		{
			Trakt_Show cr = session
				.CreateCriteria(typeof(Trakt_Show))
				.Add(Restrictions.Eq("TraktID", slug))
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
