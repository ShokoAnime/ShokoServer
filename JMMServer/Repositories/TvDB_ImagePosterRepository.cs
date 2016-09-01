using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class TvDB_ImagePosterRepository
    {
        public void Save(TvDB_ImagePoster obj)
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

        public TvDB_ImagePoster GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByID(session.Wrap(), id);
            }
        }

        public TvDB_ImagePoster GetByID(ISessionWrapper session, int id)
        {
            return session.Get<TvDB_ImagePoster>(id);
        }

        public TvDB_ImagePoster GetByTvDBID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                TvDB_ImagePoster cr = session
                    .CreateCriteria(typeof(TvDB_ImagePoster))
                    .Add(Restrictions.Eq("Id", id))
                    .UniqueResult<TvDB_ImagePoster>();
                return cr;
            }
        }

        public List<TvDB_ImagePoster> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_ImagePoster))
                    .List<TvDB_ImagePoster>();

                return new List<TvDB_ImagePoster>(objs);
            }
        }

        public List<TvDB_ImagePoster> GetBySeriesID(int seriesID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session.Wrap(), seriesID);
            }
        }

        public List<TvDB_ImagePoster> GetBySeriesID(ISessionWrapper session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_ImagePoster))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_ImagePoster>();

            return new List<TvDB_ImagePoster>(objs);
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    TvDB_ImagePoster cr = GetByID(id);
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