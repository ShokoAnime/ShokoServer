using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class Trakt_EpisodeRepository : BaseDirectRepository<Trakt_Episode, int>
{
    public List<Trakt_Episode> GetByShowID(int showID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var objs = session
                .CreateCriteria(typeof(Trakt_Episode))
                .Add(Restrictions.Eq("Trakt_ShowID", showID))
                .List<Trakt_Episode>();

            return new List<Trakt_Episode>(objs);
        });
    }

    public List<Trakt_Episode> GetByShowIDAndSeason(int showID, int seasonNumber)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var objs = session
                .CreateCriteria(typeof(Trakt_Episode))
                .Add(Restrictions.Eq("Trakt_ShowID", showID))
                .Add(Restrictions.Eq("Season", seasonNumber))
                .List<Trakt_Episode>();

            return new List<Trakt_Episode>(objs);
        });
    }

    public Trakt_Episode GetByShowIDSeasonAndEpisode(int showID, int seasonNumber, int epnumber)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var obj = session
                .CreateCriteria(typeof(Trakt_Episode))
                .Add(Restrictions.Eq("Trakt_ShowID", showID))
                .Add(Restrictions.Eq("Season", seasonNumber))
                .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                .UniqueResult<Trakt_Episode>();

            return obj;
        });
    }
}
