using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_ShowRepository : BaseDirectRepository<TMDB_Show, int>
{
    public TMDB_Show? GetByTmdbShowID(int tmdbShowId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Show>()
                .Where(a => a.TmdbShowID == tmdbShowId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_ShowRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
