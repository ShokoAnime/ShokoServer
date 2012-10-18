using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class AniDB_CategoryRepository
	{
		public void Save(AniDB_Category obj)
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

		public AniDB_Category GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Category>(id);
			}
		}

		public AniDB_Category GetByCategoryID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Category cr = session
					.CreateCriteria(typeof(AniDB_Category))
					.Add(Restrictions.Eq("CategoryID", id))
					.UniqueResult<AniDB_Category>();
				return cr;
			}
		}

		public List<AniDB_Category> GetByAnimeID(int animeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{

				var cats = session.CreateQuery("Select cat FROM AniDB_Category as cat, AniDB_Anime_Category as xref WHERE cat.CategoryID = xref.CategoryID AND xref.AnimeID= :animeID")
					.SetParameter("animeID", animeID)
					.List<AniDB_Category>();

				/*var cats = session
					.CreateCriteria(typeof(AniDB_Category))
					.Add(Restrictions.Eq("AnimeID", id))
					.AddOrder(Order.Desc("Weighting"))
					.List<AniDB_Category>();*/

				return new List<AniDB_Category>(cats);
			}
		}

		public List<AniDB_Category> GetByAnimeID(ISession session, int animeID)
		{
			var cats = session.CreateQuery("Select cat FROM AniDB_Category as cat, AniDB_Anime_Category as xref WHERE cat.CategoryID = xref.CategoryID AND xref.AnimeID= :animeID")
				.SetParameter("animeID", animeID)
				.List<AniDB_Category>();

			return new List<AniDB_Category>(cats);
		}

		public List<AniDB_Category> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Category))
					.List<AniDB_Category>();

				return new List<AniDB_Category>(objs); ;
			}
		}

		public List<AniDB_Category> GetByAnimeGroupID(int animeGroupID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				string sql = "";
				sql += " FROM AniDB_Category ac ";
				sql += " WHERE ac.CategoryID in";
				sql += " (";
				sql += " SELECT aac.CategoryID ";
				sql += " FROM AniDB_Anime_Category aac, AnimeSeries aser";
				sql += " WHERE aac.AnimeID = aser.AniDB_ID";
				sql += " AND aser.AnimeGroupID = :groupid";
				sql += " )";

				var vidfiles = session.CreateQuery(sql)
					.SetParameter("groupid", animeGroupID)
					.List<AniDB_Category>();

				return new List<AniDB_Category>(vidfiles);
			}
		}

		/// <summary>
		/// Gets all the categories, but only if we have the anime locally
		/// </summary>
		/// <returns></returns>
		public List<AniDB_Category> GetAllForLocalSeries()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var animeCats = session.CreateQuery("FROM AniDB_Category ac WHERE ac.CategoryID in (SELECT aac.CategoryID FROM AniDB_Anime_Category aac, AnimeSeries aser WHERE aac.AnimeID = aser.AniDB_ID)")
					.List<AniDB_Category>();

				return new List<AniDB_Category>(animeCats);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Category cr = GetByID(id);
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
