using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class AniDB_Anime_DefaultImageRepository
	{
		public void Save(AniDB_Anime_DefaultImage obj)
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

		public AniDB_Anime_DefaultImage GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Anime_DefaultImage>(id);
			}
		}

		public List<AniDB_Anime_DefaultImage> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Anime_DefaultImage))
					.List<AniDB_Anime_DefaultImage>();

				return new List<AniDB_Anime_DefaultImage>(objs); ;
			}
		}

		public AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(int animeid, int imageType)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Anime_DefaultImage cr = session
					.CreateCriteria(typeof(AniDB_Anime_DefaultImage))
					.Add(Restrictions.Eq("AnimeID", animeid))
					.Add(Restrictions.Eq("ImageType", imageType))
					.UniqueResult<AniDB_Anime_DefaultImage>();
				return cr;
			}
		}

		public List<AniDB_Anime_DefaultImage> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cats = session
					.CreateCriteria(typeof(AniDB_Anime_DefaultImage))
					.Add(Restrictions.Eq("AnimeID", id))
					.List<AniDB_Anime_DefaultImage>();

				return new List<AniDB_Anime_DefaultImage>(cats);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Anime_DefaultImage cr = GetByID(id);
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
