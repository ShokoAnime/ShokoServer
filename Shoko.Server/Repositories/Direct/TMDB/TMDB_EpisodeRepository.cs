using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_EpisodeRepository : BaseDirectRepository<TMDB_Episode, int>
{
    public IReadOnlyList<TMDB_Episode> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode>()
                .Where(a => a.TmdbShowID == showId)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode> GetByTmdbSeasonID(int seasonId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode>()
                .Where(a => a.TmdbSeasonID == seasonId)
                .ToList();
        });
    }

    public TMDB_Episode? GetByTmdbEpisodeID(int episodeId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode>()
                .Where(a => a.TmdbEpisodeID == episodeId)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
