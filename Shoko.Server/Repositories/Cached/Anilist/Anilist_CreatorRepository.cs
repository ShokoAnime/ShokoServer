using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_CreatorRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Creator, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Creator, int>? _anilistCreatorIDs;

    protected override int SelectKey(Anilist_Creator entity)
        => entity.Anilist_CreatorID;

    public override void PopulateIndexes()
    {
        _anilistCreatorIDs = Cache.CreateIndex(a => a.AnilistCreatorID);
    }

    public Anilist_Creator? GetByAnilistCreatorID(int anilistCreatorId)
        => _anilistCreatorIDs!.GetOne(anilistCreatorId);
}
