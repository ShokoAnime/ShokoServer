using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class ShokoImage_EntityRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<ShokoImage_Entity, int>(databaseFactory)
{
    private PocoIndex<int, ShokoImage_Entity, Guid>? _imageID;

    // Only the cross-references of images linked to another primary image, which few are.
    private PocoIndex<int, ShokoImage_Entity, Guid>? _linkedPrimaryImageID;

    private PocoIndex<int, ShokoImage_Entity, (MetadataSource, MetadataEntityType, string)>? _entitiesByID;

    protected override int SelectKey(ShokoImage_Entity entity)
        => entity.ID;

    public override void PopulateIndexes()
    {
        _imageID = Cache.CreateIndex(a => a.ImageID);
        _linkedPrimaryImageID = Cache.CreateIndex(a => a.PrimaryImageID == a.ImageID ? [] : (IReadOnlyList<Guid>)[a.PrimaryImageID]);
        _entitiesByID = Cache.CreateIndex(a => (a.EntitySource, a.EntityType, a.EntityID));
    }

    public IReadOnlyList<ShokoImage_Entity> GetByImageID(Guid imageId)
        => _imageID!.GetMultiple(imageId);

    public IReadOnlyList<ShokoImage_Entity> GetByPrimaryImageID(Guid imageId)
    {
        var xrefs = _imageID!.GetMultiple(imageId).Where(xref => xref.PrimaryImageID == imageId).ToList();
        xrefs.AddRange(_linkedPrimaryImageID!.GetMultiple(imageId));
        return xrefs;
    }

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(MetadataSource entitySource, MetadataEntityType entityType)
        => GetAll().Where(xref => xref.EntitySource == entitySource && xref.EntityType == entityType).ToList();

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(MetadataSource entitySource, MetadataEntityType entityType, string entityID)
        => _entitiesByID!.GetMultiple((entitySource, entityType, entityID));

    public IReadOnlyList<ShokoImage_Entity> GetByEntity(MetadataGuid entityID)
        => _entitiesByID!.GetMultiple((entityID.Source, entityID.EntityType, entityID.ID));

    public IReadOnlyList<ShokoImage_Entity> GetByEntityForType(MetadataSource entitySource, MetadataEntityType entityType, string entityId, ImageEntityType imageType)
        => _entitiesByID!.GetMultiple((entitySource, entityType, entityId)).Where(xref => xref.ImageType == imageType).ToList();
}
