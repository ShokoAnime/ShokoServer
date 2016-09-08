using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class TvDB_SeriesRepository
    {
        public void Save(TvDB_Series obj)
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

        public TvDB_Series GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<TvDB_Series>(id);
            }
        }

        public TvDB_Series GetByTvDBID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTvDBID(session.Wrap(), id);
            }
        }

        public TvDB_Series GetByTvDBID(ISessionWrapper session, int id)
        {
            TvDB_Series cr = session
                .CreateCriteria(typeof(TvDB_Series))
                .Add(Restrictions.Eq("SeriesID", id))
                .UniqueResult<TvDB_Series>();
            return cr;
        }

        public List<TvDB_Series> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(TvDB_Series))
                    .List<TvDB_Series>();

                return new List<TvDB_Series>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    TvDB_Series cr = GetByID(id);
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