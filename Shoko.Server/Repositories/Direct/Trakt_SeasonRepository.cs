using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class Trakt_SeasonRepository : BaseDirectRepository<Trakt_Season, int>
{
    public List<Trakt_Season> GetByShowID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<Trakt_Season>()
                .Where(a => a.Trakt_ShowID == id)
                .ToList();
        });
    }

    public Trakt_Season GetByShowIDAndSeason(int id, int season)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByShowIDAndSeasonUnsafe(session, id, season);
        });
    }

    public Trakt_Season GetByShowIDAndSeason(ISession session, int id, int season)
    {
        return Lock(() => GetByShowIDAndSeasonUnsafe(session, id, season));
    }

    private static Trakt_Season GetByShowIDAndSeasonUnsafe(ISession session, int id, int season)
    {
        return session
            .Query<Trakt_Season>()
            .Where(a => a.Trakt_ShowID == id && a.Season == season)
            .Take(1).SingleOrDefault();
    }
}
