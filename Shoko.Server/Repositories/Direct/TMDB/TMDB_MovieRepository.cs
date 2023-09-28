using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_MovieRepository : BaseDirectRepository<TMDB_Movie, int>
{
    public TMDB_Movie? GetByTmdbMovieID(int tmdbMovieId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie>()
                .Where(a => a.TmdbMovieID == tmdbMovieId)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
