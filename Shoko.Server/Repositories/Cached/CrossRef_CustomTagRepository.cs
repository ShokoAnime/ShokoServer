using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_CustomTagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_CustomTag, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_CustomTag, (int customTagID, int entityType)>? _customTagIDs;
    private PocoIndex<int, CrossRef_CustomTag, (int entityID, int entityType)>? _entityIDandType;

    protected override int SelectKey(CrossRef_CustomTag entity)
        => entity.CrossRef_CustomTagID;

    public override void PopulateIndexes()
    {
        _customTagIDs = Cache.CreateIndex(a => (a.CustomTagID, a.CrossRefType));
        _entityIDandType = Cache.CreateIndex(a => (a.CrossRefID, a.CrossRefType));
    }

    public IReadOnlyList<CrossRef_CustomTag> GetByCustomTagID(int customTagID)
        => ReadLock(() => _customTagIDs!.GetMultiple((customTagID, 1)));

    public IReadOnlyList<CrossRef_CustomTag> GetByAnimeID(int animeID)
        => ReadLock(() => _entityIDandType!.GetMultiple((animeID, 1)));

    public CrossRef_CustomTag? GetByUniqueID(int customTagID, int animeID)
        => ReadLock(() => _entityIDandType!.GetMultiple((animeID, 1)).Where(a => a.CustomTagID == customTagID).FirstOrDefault());
}
