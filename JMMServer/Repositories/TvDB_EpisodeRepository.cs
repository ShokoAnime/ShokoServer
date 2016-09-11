using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class TvDB_EpisodeRepository
    {
        public void Save(TvDB_Episode obj)
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

        public TvDB_Episode GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<TvDB_Episode>(id);
            }
        }

        public TvDB_Episode GetByTvDBID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                TvDB_Episode cr = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .Add(Restrictions.Eq("Id", id))
                    .UniqueResult<TvDB_Episode>();
                return cr;
            }
        }

        public List<TvDB_Episode> GetBySeriesID(int seriesID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session.Wrap(), seriesID);
            }
        }

        public List<TvDB_Episode> GetBySeriesID(ISessionWrapper session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_Episode))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_Episode>();

            return new List<TvDB_Episode>(objs);
        }

        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            List<int> seasonNumbers = new List<int>();
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .Add(Restrictions.Eq("SeriesID", seriesID))
                    .AddOrder(Order.Asc("SeasonNumber"))
                    .List<TvDB_Episode>();

                List<TvDB_Episode> eps = new List<TvDB_Episode>(objs);

                foreach (TvDB_Episode ep in eps)
                {
                    if (!seasonNumbers.Contains(ep.SeasonNumber))
                        seasonNumbers.Add(ep.SeasonNumber);
                }
            }

            return seasonNumbers;
        }

        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumber(int seriesID, int seasonNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .Add(Restrictions.Eq("SeriesID", seriesID))
                    .Add(Restrictions.Eq("SeasonNumber", seasonNumber))
                    .List<TvDB_Episode>();

                return new List<TvDB_Episode>(objs);
            }
        }

        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumberSorted(int seriesID, int seasonNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .Add(Restrictions.Eq("SeriesID", seriesID))
                    .Add(Restrictions.Eq("SeasonNumber", seasonNumber))
                    .AddOrder(Order.Asc("EpisodeNumber"))
                    .List<TvDB_Episode>();

                return new List<TvDB_Episode>(objs);
            }
        }

        public List<TvDB_Episode> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .List<TvDB_Episode>();

                return new List<TvDB_Episode>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    TvDB_Episode cr = GetByID(id);
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