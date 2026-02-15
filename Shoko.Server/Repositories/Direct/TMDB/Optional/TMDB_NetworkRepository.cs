#nullable enable
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_NetworkRepository : BaseDirectRepository<TMDB_Network, int>
{
    public TMDB_Network? GetByTmdbNetworkID(int tmdbNetworkId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Network>()
                .Where(a => a.TmdbNetworkID == tmdbNetworkId)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public TMDB_NetworkRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
