using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_TagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Tag, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Tag, int>? _anilistTagIDs;

    protected override int SelectKey(Anilist_Tag entity)
        => entity.Anilist_TagID;

    public override void PopulateIndexes()
    {
        _anilistTagIDs = Cache.CreateIndex(a => a.AnilistTagID);
    }

    public Anilist_Tag? GetByAnilistTagID(int anilistTagId)
        => _anilistTagIDs!.GetOne(anilistTagId);
}
