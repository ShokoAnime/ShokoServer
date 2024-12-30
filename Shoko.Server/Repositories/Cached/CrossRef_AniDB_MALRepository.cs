using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_MALRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_MAL, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_MAL, int>? _animeIDs;

    private PocoIndex<int, CrossRef_AniDB_MAL, int>? _malIDs;

    protected override int SelectKey(CrossRef_AniDB_MAL entity)
        => entity.CrossRef_AniDB_MALID;

    public override void PopulateIndexes()
    {
        _malIDs = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.MALID);
        _animeIDs = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.AnimeID);
    }

    public IReadOnlyList<CrossRef_AniDB_MAL> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID).OrderBy(a => a.StartEpisodeType).ThenBy(a => a.StartEpisodeNumber).ToList());

    public IReadOnlyList<CrossRef_AniDB_MAL> GetByMALID(int malID)
        => ReadLock(() => _malIDs!.GetMultiple(malID));
}
