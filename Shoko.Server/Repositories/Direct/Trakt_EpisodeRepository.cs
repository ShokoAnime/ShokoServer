using System.Collections.Generic;
using System.Linq;
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
            return session
                .Query<Trakt_Episode>()
                .Where(a => a.Trakt_ShowID == showID)
                .ToList();
        });
    }

    public List<Trakt_Episode> GetByShowIDAndSeason(int showID, int seasonNumber)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<Trakt_Episode>()
                .Where(a => a.Trakt_ShowID == showID && a.Season == seasonNumber)
                .ToList();
        });
    }

    public Trakt_Episode GetByShowIDSeasonAndEpisode(int showID, int seasonNumber, int epnumber)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<Trakt_Episode>()
                .Where(a => a.Trakt_ShowID == showID && a.Season == seasonNumber && a.EpisodeNumber == epnumber)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
