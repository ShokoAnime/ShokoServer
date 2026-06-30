using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoImage_EntityRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<ShokoImage_Entity, int>(databaseFactory)
{
    private PocoIndex<int, ShokoImage_Entity, Guid>? _imageID;

    private PocoIndex<int, ShokoImage_Entity, Guid>? _primaryImageID;

    private PocoIndex<int, ShokoImage_Entity, (DataSource, DataEntityType)>? _entities;

    private PocoIndex<int, ShokoImage_Entity, (DataSource, DataEntityType, ImageEntityType)>? _entitiesWithType;

    private PocoIndex<int, ShokoImage_Entity, (DataSource, DataEntityType, string)>? _entitiesByID;

    private PocoIndex<int, ShokoImage_Entity, (DataSource, DataEntityType, string, ImageEntityType)>? _entitiesByIDWithType;

    protected override int SelectKey(ShokoImage_Entity entity)
        => entity.ID;

    public override void PopulateIndexes()
    {
        _imageID = Cache.CreateIndex(a => a.ImageID);
        _primaryImageID = Cache.CreateIndex(a => a.PrimaryImageID);
        _entities = Cache.CreateIndex(a => (a.EntitySource, a.EntityType));
        _entitiesWithType = Cache.CreateIndex(a => (a.EntitySource, a.EntityType, a.ImageType));
        _entitiesByID = Cache.CreateIndex(a => (a.EntitySource, a.EntityType, a.EntityID));
        _entitiesByIDWithType = Cache.CreateIndex(a => (a.EntitySource, a.EntityType, a.EntityID, a.ImageType));
    }

    public IReadOnlyList<ShokoImage_Entity> GetByImageID(Guid imageId)
        => _imageID!.GetMultiple(imageId);

    public IReadOnlyList<ShokoImage_Entity> GetByPrimaryImageID(Guid imageId)
        => _primaryImageID!.GetMultiple(imageId);

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(DataSource entitySource, DataEntityType entityType)
        => _entities!.GetMultiple((entitySource, entityType));

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(DataSource entitySource, DataEntityType entityType, string entityID)
        => _entitiesByID!.GetMultiple((entitySource, entityType, entityID));

    public IReadOnlyList<ShokoImage_Entity> GetByEntityForType(DataSource entitySource, DataEntityType entityType, ImageEntityType imageType)
        => _entitiesWithType!.GetMultiple((entitySource, entityType, imageType));

    public IReadOnlyList<ShokoImage_Entity> GetByEntityForType(DataSource entitySource, DataEntityType entityType, string entityId, ImageEntityType imageType)
        => _entitiesByIDWithType!.GetMultiple((entitySource, entityType, entityId, imageType));

    public IReadOnlyList<ShokoImage_Entity> GetPreferredImagesByEntity(DataSource entitySource, DataEntityType entityType, string entityId)
        => GetByEntity(entitySource, entityType, entityId).Where(xref => xref.IsPreferred).ToList();

    public ShokoImage_Entity? GetPreferredImageByEntityForType(DataSource entitySource, DataEntityType entityType, string entityId, ImageEntityType imageType)
        => GetByEntityForType(entitySource, entityType, entityId, imageType).SingleOrDefault(xref => xref.IsPreferred);
}
