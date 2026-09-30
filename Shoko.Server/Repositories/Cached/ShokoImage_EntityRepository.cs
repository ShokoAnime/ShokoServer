using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class ShokoImage_EntityRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<ShokoImage_Entity, int>(databaseFactory)
{
    private PocoIndex<int, ShokoImage_Entity, Guid>? _imageID;

    private PocoIndex<int, ShokoImage_Entity, Guid>? _primaryImageID;

    private PocoIndex<int, ShokoImage_Entity, (MetadataSource, MetadataEntityType)>? _entities;

    private PocoIndex<int, ShokoImage_Entity, (MetadataSource, MetadataEntityType, string)>? _entitiesByID;

    private PocoIndex<int, ShokoImage_Entity, (MetadataSource, MetadataEntityType, string, ImageEntityType)>? _entitiesByIDWithType;

    protected override int SelectKey(ShokoImage_Entity entity)
        => entity.ID;

    public override void PopulateIndexes()
    {
        _imageID = Cache.CreateIndex(a => a.ImageID);
        _primaryImageID = Cache.CreateIndex(a => a.PrimaryImageID);
        _entities = Cache.CreateIndex(a => (a.EntitySource, a.EntityType));
        _entitiesByID = Cache.CreateIndex(a => (a.EntitySource, a.EntityType, a.EntityID));
        _entitiesByIDWithType = Cache.CreateIndex(a => (a.EntitySource, a.EntityType, a.EntityID, a.ImageType));
    }

    public IReadOnlyList<ShokoImage_Entity> GetByImageID(Guid imageId)
        => _imageID!.GetMultiple(imageId);

    public IReadOnlyList<ShokoImage_Entity> GetByPrimaryImageID(Guid imageId)
        => _primaryImageID!.GetMultiple(imageId);

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(MetadataSource entitySource, MetadataEntityType entityType)
        => _entities!.GetMultiple((entitySource, entityType));

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(MetadataSource entitySource, MetadataEntityType entityType, string entityID)
        => _entitiesByID!.GetMultiple((entitySource, entityType, entityID));

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(MetadataGuid entityID)
        => _entitiesByID!.GetMultiple((entityID.Source, entityID.EntityType, entityID.ID));

    public IReadOnlyList<ShokoImage_Entity> GetByEntityForType(MetadataSource entitySource, MetadataEntityType entityType, string entityId, ImageEntityType imageType)
        => _entitiesByIDWithType!.GetMultiple((entitySource, entityType, entityId, imageType));
}
