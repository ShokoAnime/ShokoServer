using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class Trakt_EpisodeRepository : BaseDirectRepository<Trakt_Episode, int>
    {
        private Trakt_EpisodeRepository()
        {
        }

        public static Trakt_EpisodeRepository Create()
        {
            return new Trakt_EpisodeRepository();
        }

        public List<Trakt_Episode> GetByShowID(int showID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Trakt_Episode obj = session
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
            List<int> seasonNumbers = new List<int>();
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .AddOrder(Order.Asc("Season"))
                    .List<Trakt_Episode>();

                List<Trakt_Episode> eps = new List<Trakt_Episode>(objs);

                foreach (Trakt_Episode ep in eps)
                {
                    if (!seasonNumbers.Contains(ep.Season))
                        seasonNumbers.Add(ep.Season);
                }
            }

            return seasonNumbers;
        }
    }
}