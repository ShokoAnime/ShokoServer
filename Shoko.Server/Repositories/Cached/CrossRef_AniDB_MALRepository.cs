using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_MALRepository : BaseCachedRepository<CrossRef_AniDB_MAL, int>
{
    private PocoIndex<int, CrossRef_AniDB_MAL, int> _animeIDs;
    private PocoIndex<int, CrossRef_AniDB_MAL, int> _MALIDs;

    public List<CrossRef_AniDB_MAL> GetByAnimeID(int id)
    {
        return ReadLock(() =>
            _animeIDs.GetMultiple(id).OrderBy(a => a.StartEpisodeType).ThenBy(a => a.StartEpisodeNumber).ToList());
    }

    public List<CrossRef_AniDB_MAL> GetByMALID(int id)
    {
        return ReadLock(() => _MALIDs.GetMultiple(id));
    }

    protected override int SelectKey(CrossRef_AniDB_MAL entity)
    {
        return entity.CrossRef_AniDB_MALID;
    }

    public override void PopulateIndexes()
    {
        _MALIDs = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.MALID);
        _animeIDs = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
    }

    public CrossRef_AniDB_MALRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
