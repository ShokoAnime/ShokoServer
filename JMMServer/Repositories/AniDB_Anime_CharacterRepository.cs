using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class AniDB_Anime_CharacterRepository
	{
		public void Save(AniDB_Anime_Character obj)
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

		public AniDB_Anime_Character GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Anime_Character>(id);
			}
		}

		public List<AniDB_Anime_Character> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cats = session
					.CreateCriteria(typeof(AniDB_Anime_Character))
					.Add(Restrictions.Eq("AnimeID", id))
					.List<AniDB_Anime_Character>();

				return new List<AniDB_Anime_Character>(cats);
			}
		}

		public AniDB_Anime_Character GetByAnimeIDAndCharID(int animeid, int charid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Anime_Character cr = session
					.CreateCriteria(typeof(AniDB_Anime_Character))
					.Add(Restrictions.Eq("AnimeID", animeid))
					.Add(Restrictions.Eq("CharID", charid))
					.UniqueResult<AniDB_Anime_Character>();

				return cr;
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Anime_Character cr = GetByID(id);
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
