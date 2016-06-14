using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class MovieDB_MovieRepository
	{
		public void Save(MovieDB_Movie obj)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				Save(session, obj);
			}
		}

		public void Save(ISession session, MovieDB_Movie obj)
		{
			// populate the database
			using (var transaction = session.BeginTransaction())
			{
				session.SaveOrUpdate(obj);
				transaction.Commit();
			}
		}

		public MovieDB_Movie GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<MovieDB_Movie>(id);
			}
		}

		public MovieDB_Movie GetByOnlineID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByOnlineID(session, id);
			}
		}

		public MovieDB_Movie GetByOnlineID(ISession session, int id)
		{
			MovieDB_Movie cr = session
				.CreateCriteria(typeof(MovieDB_Movie))
				.Add(Restrictions.Eq("MovieId", id))
				.UniqueResult<MovieDB_Movie>();
			return cr;
		}

		public List<MovieDB_Movie> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Movie))
					.List<MovieDB_Movie>();

				return new List<MovieDB_Movie>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					MovieDB_Movie cr = GetByID(id);
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
