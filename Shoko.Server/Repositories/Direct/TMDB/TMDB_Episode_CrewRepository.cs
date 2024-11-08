using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_Episode_CrewRepository : BaseDirectRepository<TMDB_Episode_Crew, int>
{
    public IReadOnlyList<TMDB_Episode_Crew> GetByTmdbPersonID(int personId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Crew>()
                .Where(a => a.TmdbPersonID == personId)
                .OrderBy(e => e.TmdbShowID)
                .ThenBy(e => e.Department)
                .ThenBy(e => e.Job)
                .ThenBy(e => e.TmdbCreditID)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode_Crew> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Crew>()
                .Where(a => a.TmdbShowID == showId)
                .OrderBy(e => e.Department)
                .ThenBy(e => e.Job)
                .ThenBy(e => e.TmdbCreditID)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode_Crew> GetByTmdbSeasonID(int seasonId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Crew>()
                .Where(a => a.TmdbSeasonID == seasonId)
                .OrderBy(e => e.Department)
                .ThenBy(e => e.Job)
                .ThenBy(e => e.TmdbCreditID)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Episode_Crew> GetByTmdbEpisodeID(int episodeId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Episode_Crew>()
                .Where(a => a.TmdbEpisodeID == episodeId)
                .OrderBy(e => e.Department)
                .ThenBy(e => e.Job)
                .ThenBy(e => e.TmdbCreditID)
                .ToList();
        });
    }

    public TMDB_Episode_CrewRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
