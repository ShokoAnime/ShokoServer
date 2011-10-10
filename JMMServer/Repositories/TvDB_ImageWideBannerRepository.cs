using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class TvDB_ImageWideBannerRepository
	{
		public void Save(TvDB_ImageWideBanner obj)
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

		public TvDB_ImageWideBanner GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<TvDB_ImageWideBanner>(id);
			}
		}

		public TvDB_ImageWideBanner GetByTvDBID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				TvDB_ImageWideBanner cr = session
					.CreateCriteria(typeof(TvDB_ImageWideBanner))
					.Add(Restrictions.Eq("Id", id))
					.UniqueResult<TvDB_ImageWideBanner>();
				return cr;
			}
		}

		public List<TvDB_ImageWideBanner> GetBySeriesID(int seriesID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(TvDB_ImageWideBanner))
					.Add(Restrictions.Eq("SeriesID", seriesID))
					.List<TvDB_ImageWideBanner>();

				return new List<TvDB_ImageWideBanner>(objs);
			}
		}

		public List<TvDB_ImageWideBanner> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(TvDB_ImageWideBanner))
					.List<TvDB_ImageWideBanner>();

				return new List<TvDB_ImageWideBanner>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					TvDB_ImageWideBanner cr = GetByID(id);
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
