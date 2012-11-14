using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class CrossRef_AniDB_TvDBRepository
	{
		public void Save(CrossRef_AniDB_TvDB obj)
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

		public CrossRef_AniDB_TvDB GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<CrossRef_AniDB_TvDB>(id);
			}
		}

		public CrossRef_AniDB_TvDB GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByAnimeID(session, id);
			}
		}

		public CrossRef_AniDB_TvDB GetByAnimeID(ISession session, int id)
		{
			CrossRef_AniDB_TvDB cr = session
				.CreateCriteria(typeof(CrossRef_AniDB_TvDB))
				.Add(Restrictions.Eq("AnimeID", id))
				.UniqueResult<CrossRef_AniDB_TvDB>();
			return cr;
		}

		public CrossRef_AniDB_TvDB GetByTvDBID(int id, int season)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				CrossRef_AniDB_TvDB cr = session
					.CreateCriteria(typeof(CrossRef_AniDB_TvDB))
					.Add(Restrictions.Eq("TvDBID", id))
					.Add(Restrictions.Eq("TvDBSeasonNumber", season))
					.UniqueResult<CrossRef_AniDB_TvDB>();
				return cr;
			}
		}

		public List<CrossRef_AniDB_TvDB> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var series = session
					.CreateCriteria(typeof(CrossRef_AniDB_TvDB))
					.List<CrossRef_AniDB_TvDB>();

				return new List<CrossRef_AniDB_TvDB>(series);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					CrossRef_AniDB_TvDB cr = GetByID(id);
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
