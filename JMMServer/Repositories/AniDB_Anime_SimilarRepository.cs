using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class AniDB_Anime_SimilarRepository
	{
		public void Save(AniDB_Anime_Similar obj)
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

		public AniDB_Anime_Similar GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Anime_Similar>(id);
			}
		}

		public AniDB_Anime_Similar GetByAnimeIDAndSimilarID(int animeid, int similaranimeid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Anime_Similar cr = session
					.CreateCriteria(typeof(AniDB_Anime_Similar))
					.Add(Restrictions.Eq("AnimeID", animeid))
					.Add(Restrictions.Eq("SimilarAnimeID", similaranimeid))
					.UniqueResult<AniDB_Anime_Similar>();
				return cr;
			}
		}

		public List<AniDB_Anime_Similar> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cats = session
					.CreateCriteria(typeof(AniDB_Anime_Similar))
					.Add(Restrictions.Eq("AnimeID", id))
					.List<AniDB_Anime_Similar>();

				return new List<AniDB_Anime_Similar>(cats);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Anime_Similar cr = GetByID(id);
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
