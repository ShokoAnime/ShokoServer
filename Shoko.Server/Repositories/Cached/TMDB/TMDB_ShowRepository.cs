#nullable enable
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_ShowRepository : BaseCachedRepository<TMDB_Show, int>
{
    protected override int SelectKey(TMDB_Show entity) => entity.Id;
    private PocoIndex<int, TMDB_Show, int> _showIDs = null!;

    public override void PopulateIndexes()
    {
        _showIDs = Cache.CreateIndex(a => a.TmdbShowID);
    }

    public TMDB_Show? GetByTmdbShowID(int tmdbShowId)
    {
        return _showIDs.GetOne(tmdbShowId);
    }

    public TMDB_ShowRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
