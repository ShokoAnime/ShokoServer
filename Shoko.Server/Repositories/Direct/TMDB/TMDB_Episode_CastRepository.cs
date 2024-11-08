using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_Episode_CastRepository : BaseDirectRepository<TMDB_Episode_Cast, int>
{
    public IReadOnlyList<TMDB_Episode_Cast> GetByTmdbPersonID(int personId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Cast>()
                .Where(a => a.TmdbPersonID == personId)
                .OrderBy(e => e.TmdbShowID)
                .ThenBy(e => e.TmdbEpisodeID)
                .ThenBy(e => e.Ordering)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode_Cast> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Cast>()
                .Where(a => a.TmdbShowID == showId)
                .OrderBy(e => e.TmdbEpisodeID)
                .ThenBy(e => e.Ordering)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode_Cast> GetByTmdbSeasonID(int seasonId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Cast>()
                .Where(a => a.TmdbSeasonID == seasonId)
                .OrderBy(e => e.TmdbEpisodeID)
                .ThenBy(e => e.Ordering)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode_Cast> GetByTmdbEpisodeID(int episodeId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Cast>()
                .Where(a => a.TmdbEpisodeID == episodeId)
                .OrderBy(e => e.Ordering)
                .ToList();
        });
    }

    public TMDB_Episode_CastRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
