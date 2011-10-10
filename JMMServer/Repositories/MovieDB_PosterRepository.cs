using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class MovieDB_PosterRepository
	{
		public void Save(MovieDB_Poster obj)
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

		public MovieDB_Poster GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<MovieDB_Poster>(id);
			}
		}

		public MovieDB_Poster GetByOnlineID(string id, string imageSize)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				MovieDB_Poster cr = session
					.CreateCriteria(typeof(MovieDB_Poster))
					.Add(Restrictions.Eq("ImageID", id))
					.Add(Restrictions.Eq("ImageSize", imageSize))
					.UniqueResult<MovieDB_Poster>();
				return cr;
			}
		}

		public List<MovieDB_Poster> GetByMovieID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Poster))
					.Add(Restrictions.Eq("MovieId", id))
					.List<MovieDB_Poster>();

				return new List<MovieDB_Poster>(objs);
			}
		}

		public List<MovieDB_Poster> GetAllOriginal()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Poster))
					.Add(Restrictions.Eq("ImageSize", "original"))
					.List<MovieDB_Poster>();

				return new List<MovieDB_Poster>(objs);
			}
		}

		public List<MovieDB_Poster> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(MovieDB_Poster))
					.List<MovieDB_Poster>();

				return new List<MovieDB_Poster>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					MovieDB_Poster cr = GetByID(id);
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
