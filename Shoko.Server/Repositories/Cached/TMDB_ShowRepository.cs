using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class TMDB_ShowRepository : BaseCachedRepository<TMDB_Show, int>
{

    private PocoIndex<int, TMDB_Show, int> TmdbShowIds;

    public override void PopulateIndexes()
    {
        TmdbShowIds = new PocoIndex<int, TMDB_Show, int>(Cache, a => a.TmdbShowID);
    }

    public TMDB_Show? GetByTmdbShowID(int tmdbShowId)
    {
        return ReadLock(() => TmdbShowIds.GetOne(tmdbShowId));
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(TMDB_Show entity)
    {
        return entity.TMDB_ShowID;
    }

    public TMDB_ShowRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
