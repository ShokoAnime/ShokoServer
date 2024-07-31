using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_Movie_CrewRepository : BaseDirectRepository<TMDB_Movie_Crew, int>
{
    public IReadOnlyList<TMDB_Movie_Crew> GetByTmdbPersonID(int personId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie_Crew>()
                .Where(a => a.TmdbPersonID == personId)
                .OrderBy(e => e.TmdbMovieID)
                .ThenBy(e => e.Department)
                .ThenBy(e => e.Job)
                .ThenBy(e => e.TmdbCreditID)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Movie_Crew> GetByTmdbMovieID(int movieId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Movie_Crew>()
                .Where(a => a.TmdbMovieID == movieId)
                .OrderBy(e => e.Department)
                .ThenBy(e => e.Job)
                .ThenBy(e => e.TmdbCreditID)
                .ToList();
        });
    }

    public TMDB_Movie_CrewRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
