using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
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
                return GetByID(session, id);
            }
        }

        public TvDB_ImageWideBanner GetByID(ISession session, int id)
        {
            return session.Get<TvDB_ImageWideBanner>(id);
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
                return GetBySeriesID(session, seriesID);
            }
        }

        public List<TvDB_ImageWideBanner> GetBySeriesID(ISession session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_ImageWideBanner))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_ImageWideBanner>();

            List<TvDB_ImageWideBanner> temp = (List<TvDB_ImageWideBanner>) objs;
            List<TvDB_ImageWideBanner> results = new List<TvDB_ImageWideBanner>();
            foreach (TvDB_ImageWideBanner pic in temp)
            {
                if (!System.IO.File.Exists(pic.FullImagePath))
                {
                    Delete(pic.TvDB_ImageWideBannerID);
                }
                else
                {
                    results.Add(pic);
                }
            }

            return new List<TvDB_ImageWideBanner>(results);
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