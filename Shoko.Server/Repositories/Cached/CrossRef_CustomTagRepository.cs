using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_CustomTagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_CustomTag, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_CustomTag, int>? _customTagIDs;
    private PocoIndex<int, CrossRef_CustomTag, (int entityID, CustomTagCrossRefType entityType)>? _entityIDandType;

    protected override int SelectKey(CrossRef_CustomTag entity)
        => entity.CrossRef_CustomTagID;

    public override void PopulateIndexes()
    {
        _customTagIDs = Cache.CreateIndex(a => a.CustomTagID);
        _entityIDandType = Cache.CreateIndex(a => (a.CrossRefID, (CustomTagCrossRefType)a.CrossRefType));
    }

    public IReadOnlyList<CrossRef_CustomTag> GetByCustomTagID(int customTagID)
        => ReadLock(() => _customTagIDs!.GetMultiple(customTagID));

    public IReadOnlyList<CrossRef_CustomTag> GetByEntityIDAndType(int entityID, CustomTagCrossRefType entityType)
        => ReadLock(() => _entityIDandType!.GetMultiple((entityID, entityType)));

    public IReadOnlyList<CrossRef_CustomTag> GetByAnimeID(int animeID)
        => GetByEntityIDAndType(animeID, CustomTagCrossRefType.Anime);

    public IReadOnlyList<CrossRef_CustomTag> GetByUniqueID(int customTagID, CustomTagCrossRefType entityType, int entityID)
        => GetByEntityIDAndType(entityID, entityType)
            .Where(a => a.CustomTagID == customTagID)
            .ToList();
}
