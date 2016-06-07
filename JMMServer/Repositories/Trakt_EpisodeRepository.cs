using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class Trakt_EpisodeRepository
    {
        public void Save(Trakt_Episode obj)
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

        public Trakt_Episode GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<Trakt_Episode>(id);
            }
        }

        public List<Trakt_Episode> GetByShowID(int showID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .List<Trakt_Episode>();

                return new List<Trakt_Episode>(objs);
            }
        }

        public List<Trakt_Episode> GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber))
                    .List<Trakt_Episode>();

                return new List<Trakt_Episode>(objs);
            }
        }

        public Trakt_Episode GetByShowIDSeasonAndEpisode(int showID, int seasonNumber, int epnumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var obj = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .UniqueResult<Trakt_Episode>();

                return obj;
            }
        }

        public List<int> GetSeasonNumbersForSeries(int showID)
        {
            var seasonNumbers = new List<int>();
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .AddOrder(Order.Asc("Season"))
                    .List<Trakt_Episode>();

                var eps = new List<Trakt_Episode>(objs);

                foreach (var ep in eps)
                {
                    if (!seasonNumbers.Contains(ep.Season))
                        seasonNumbers.Add(ep.Season);
                }
            }

            return seasonNumbers;
        }

        public List<Trakt_Episode> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .List<Trakt_Episode>();

                return new List<Trakt_Episode>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
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