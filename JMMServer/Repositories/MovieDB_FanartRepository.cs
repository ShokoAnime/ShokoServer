using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class MovieDB_FanartRepository
	{
		public void Save(MovieDB_Fanart obj)
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

		public MovieDB_Fanart GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<MovieDB_Fanart>(id);
			}
		}

		public MovieDB_Fanart GetByOnlineID(string id, string imageSize)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				MovieDB_Fanart cr = session
					.CreateCriteria(typeof(MovieDB_Fanart))
					.Add(Restrictions.Eq("ImageID", id))
					.Add(Restrictions.Eq("ImageSize", imageSize))
					.UniqueResult<MovieDB_Fanart>();
				return cr;
			}
		}

		public List<MovieDB_Fanart> GetByMovieID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Fanart))
					.Add(Restrictions.Eq("MovieId", id))
					.List<MovieDB_Fanart>();

				return new List<MovieDB_Fanart>(objs);
			}
		}

		public List<MovieDB_Fanart> GetAllOriginal()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Fanart))
					.Add(Restrictions.Eq("ImageSize", "original"))
					.List<MovieDB_Fanart>();

				return new List<MovieDB_Fanart>(objs);
			}
		}

		public List<MovieDB_Fanart> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Fanart))
					.List<MovieDB_Fanart>();

				return new List<MovieDB_Fanart>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					MovieDB_Fanart cr = GetByID(id);
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
