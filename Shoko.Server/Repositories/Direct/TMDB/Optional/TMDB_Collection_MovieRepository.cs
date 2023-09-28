using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_Collection_MovieRepository : BaseDirectRepository<TMDB_Collection_Movie, int>
{
    public IReadOnlyList<TMDB_Collection_Movie> GetByTmdbCollectionID(int collectinoId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Collection_Movie>()
                .Where(a => a.TmdbCollectionID == collectinoId)
                .ToList();
        });
    }

    public TMDB_Collection_Movie? GetByTmdbMovieID(int movieId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Collection_Movie>()
                .Where(a => a.TmdbMovieID == movieId)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
