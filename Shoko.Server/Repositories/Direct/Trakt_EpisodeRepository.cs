using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NHibernate.Engine.Query;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Providers;

namespace Shoko.Server.Repositories.Direct
{
    public class Trakt_EpisodeRepository : BaseDirectRepository<Trakt_Episode, int>, IEpisodeGenericRepo
    {
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
        public Trakt_Episode GetByShowIDSeasonAndEpisode(string providerEpisodeId)
        {
            string[] vals = providerEpisodeId.Split("_");
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Trakt_Episode obj = session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", int.Parse(vals[0])))
                    .Add(Restrictions.Eq("Season", int.Parse(vals[1])))
                    .Add(Restrictions.Eq("EpisodeNumber", int.Parse(vals[2])))
                    .UniqueResult<Trakt_Episode>();

                return obj;
            }
        }
        public int GetNumberOfEpisodesForSeason(int showID, int seasonNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return (int)session
                    .CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .Add(Restrictions.Eq("Season", seasonNumber)).SetProjection(Projections.RowCount()).UniqueResult();
            }
        }

        public int getLastSeasonForSeries(int showID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {

                int? result = session.CreateCriteria(typeof(Trakt_Episode))
                    .Add(Restrictions.Eq("Trakt_ShowID", showID))
                    .SetProjection(Projections.Max("Season")).UniqueResult<int?>();
                if (result == null)
                    result = -1;
                return result.Value;
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

        public List<GenericEpisode> GetByProviderID(string providerId) => this.GetByShowID(int.Parse(providerId)).Select(a => new GenericEpisode(a)).ToList();

        public int GetNumberOfEpisodesForSeason(string providerId, int season) => this.GetNumberOfEpisodesForSeason(int.Parse(providerId), season);

        public int GetLastSeasonForSeries(string providerId) => this.getLastSeasonForSeries(int.Parse(providerId));

        public GenericEpisode GetByEpisodeProviderID(string episodeproviderId)
        {
            Trakt_Episode ep = this.GetByShowIDSeasonAndEpisode(episodeproviderId);
            if (ep == null)
                return null;
            return new GenericEpisode(ep);
        }

        public GenericEpisode GetByProviderIdSeasonAnEpNumber(string providerId, int season, int epNumber)
        {
            Trakt_Episode ep = this.GetByShowIDSeasonAndEpisode(int.Parse(providerId), season, epNumber);
            if (ep == null)
                return null;
            return new GenericEpisode(ep);
        }
    }
}