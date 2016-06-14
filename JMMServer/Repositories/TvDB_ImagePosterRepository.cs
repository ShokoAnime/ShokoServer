using System.Collections.Generic;
using JMMServer.Entities;
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
                return GetByID(session, id);
            }
        }

        public TvDB_ImagePoster GetByID(ISession session, int id)
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
                return GetBySeriesID(session, seriesID);
            }
        }

        public List<TvDB_ImagePoster> GetBySeriesID(ISession session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_ImagePoster))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_ImagePoster>();

            List<TvDB_ImagePoster> temp = (List<TvDB_ImagePoster>) objs;
            List<TvDB_ImagePoster> results = new List<TvDB_ImagePoster>();
            foreach (TvDB_ImagePoster pic in temp)
            {
                if (!System.IO.File.Exists(pic.FullImagePath))
                {
                    if (System.IO.File.Exists(pic.FullImagePath))
                    {
                        System.IO.File.Delete(pic.FullImagePath);
                    }
                    Delete(pic.TvDB_ImagePosterID);
                }
                else
                {
                    results.Add(pic);
                }
            }

            return new List<TvDB_ImagePoster>(results);
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