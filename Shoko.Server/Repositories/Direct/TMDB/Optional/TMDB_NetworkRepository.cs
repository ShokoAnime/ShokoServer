using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_NetworkRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_Network, int>(databaseFactory)
{
    public TMDB_Network? GetByTmdbNetworkID(int tmdbNetworkId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Network>()
            .Where(a => a.TmdbNetworkID == tmdbNetworkId)
            .Take(1)
            .SingleOrDefault();
    }
}
