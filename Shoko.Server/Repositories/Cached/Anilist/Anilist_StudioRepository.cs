using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_StudioRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Studio, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Studio, int>? _anilistStudioIDs;

    protected override int SelectKey(Anilist_Studio entity)
        => entity.Anilist_StudioID;

    public override void PopulateIndexes()
    {
        _anilistStudioIDs = Cache.CreateIndex(a => a.AnilistStudioID);
    }

    public Anilist_Studio? GetByAnilistStudioID(int anilistStudioId)
        => _anilistStudioIDs!.GetOne(anilistStudioId);
}
