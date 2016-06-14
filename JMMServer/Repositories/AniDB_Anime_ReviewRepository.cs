using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class AniDB_Anime_ReviewRepository
	{
		public void Save(AniDB_Anime_Review obj)
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

		public AniDB_Anime_Review GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Anime_Review>(id);
			}
		}

		public AniDB_Anime_Review GetByAnimeIDAndReviewID(int animeid, int reviewid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Anime_Review cr = session
					.CreateCriteria(typeof(AniDB_Anime_Review))
					.Add(Restrictions.Eq("AnimeID", animeid))
					.Add(Restrictions.Eq("ReviewID", reviewid))
					.UniqueResult<AniDB_Anime_Review>();
				return cr;
			}
		}

		public List<AniDB_Anime_Review> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cats = session
					.CreateCriteria(typeof(AniDB_Anime_Review))
					.Add(Restrictions.Eq("AnimeID", id))
					.List<AniDB_Anime_Review>();

				return new List<AniDB_Anime_Review>(cats);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Anime_Review cr = GetByID(id);
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
