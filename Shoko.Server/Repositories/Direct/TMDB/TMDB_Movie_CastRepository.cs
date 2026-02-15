#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB;

public class TMDB_Movie_CastRepository : BaseDirectRepository<TMDB_Movie_Cast, int>
{
    public IReadOnlyList<TMDB_Movie_Cast> GetByTmdbPersonID(int personId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie_Cast>()
                .Where(a => a.TmdbPersonID == personId)
                .OrderBy(e => e.TmdbMovieID)
                .ThenBy(e => e.Ordering)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Movie_Cast> GetByTmdbMovieID(int movieId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie_Cast>()
                .Where(a => a.TmdbMovieID == movieId)
                .OrderBy(e => e.Ordering)
                .ToList();
        });
    }

    public TMDB_Movie_CastRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
