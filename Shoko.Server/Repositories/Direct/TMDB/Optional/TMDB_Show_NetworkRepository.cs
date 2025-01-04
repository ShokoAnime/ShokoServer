#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_Show_NetworkRepository : BaseDirectRepository<TMDB_Show_Network, int>
{
    public IReadOnlyList<TMDB_Show_Network> GetByTmdbNetworkID(int networkId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Show_Network>()
                .Where(a => a.TmdbNetworkID == networkId)
                .OrderBy(e => e.TmdbShowID)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_Show_Network> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Show_Network>()
                .Where(a => a.TmdbShowID == showId)
                .OrderBy(e => e.Ordering)
                .ToList();
        });
    }

    public TMDB_Show_NetworkRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
