using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class AniDB_Anime_CategoryRepository
	{
		public void Save(AniDB_Anime_Category obj)
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

		public AniDB_Anime_Category GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Anime_Category>(id);
			}
		}

		public List<AniDB_Anime_Category> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Anime_Category))
					.List<AniDB_Anime_Category>();

				return new List<AniDB_Anime_Category>(objs); ;
			}
		}

		public AniDB_Anime_Category GetByAnimeIDAndCategoryID(int animeid, int catid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Anime_Category cr = session
					.CreateCriteria(typeof(AniDB_Anime_Category))
					.Add(Restrictions.Eq("AnimeID", animeid))
					.Add(Restrictions.Eq("CategoryID", catid))
					.UniqueResult<AniDB_Anime_Category>();
				return cr;
			}
		}

		public List<AniDB_Anime_Category> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cats = session
					.CreateCriteria(typeof(AniDB_Anime_Category))
					.Add(Restrictions.Eq("AnimeID", id))
					.AddOrder(Order.Desc("Weighting"))
					.List<AniDB_Anime_Category>();

				return new List<AniDB_Anime_Category>(cats);
			}
		}

		public List<AniDB_Anime_Category> GetByAnimeID(ISession session, int id)
		{
			var cats = session
				.CreateCriteria(typeof(AniDB_Anime_Category))
				.Add(Restrictions.Eq("AnimeID", id))
				.AddOrder(Order.Desc("Weighting"))
				.List<AniDB_Anime_Category>();

			return new List<AniDB_Anime_Category>(cats);
		}

		/// <summary>
		/// Gets all the anime categories, but only if we have the anime locally
		/// </summary>
		/// <returns></returns>
		public List<AniDB_Anime_Category> GetAllForLocalSeries()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var animeCats = session.CreateQuery("FROM AniDB_Anime_Category aac WHERE aac.AnimeID in (Select aser.AniDB_ID From AnimeSeries aser)")
					.List<AniDB_Anime_Category>();

				return new List<AniDB_Anime_Category>(animeCats);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Anime_Category cr = GetByID(id);
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
