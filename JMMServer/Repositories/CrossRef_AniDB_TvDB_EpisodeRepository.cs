using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class CrossRef_AniDB_TvDB_EpisodeRepository
	{
		public void Save(CrossRef_AniDB_TvDB_Episode obj)
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

		public CrossRef_AniDB_TvDB_Episode GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<CrossRef_AniDB_TvDB_Episode>(id);
			}
		}

		public CrossRef_AniDB_TvDB_Episode GetByAniDBEpisodeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				CrossRef_AniDB_TvDB_Episode cr = session
					.CreateCriteria(typeof(CrossRef_AniDB_TvDB_Episode))
					.Add(Restrictions.Eq("AniDBEpisodeID", id))
					.UniqueResult<CrossRef_AniDB_TvDB_Episode>();
				return cr;
			}
		}

		public List<CrossRef_AniDB_TvDB_Episode> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(CrossRef_AniDB_TvDB_Episode))
					.Add(Restrictions.Eq("AnimeID", id))
					.List<CrossRef_AniDB_TvDB_Episode>();

				return new List<CrossRef_AniDB_TvDB_Episode>(objs);
			}
		}

		public List<CrossRef_AniDB_TvDB_Episode> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var series = session
					.CreateCriteria(typeof(CrossRef_AniDB_TvDB_Episode))
					.List<CrossRef_AniDB_TvDB_Episode>();

				return new List<CrossRef_AniDB_TvDB_Episode>(series);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					CrossRef_AniDB_TvDB_Episode cr = GetByID(id);
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
