using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoImageRepository : BaseCachedRepository<ShokoImage, Guid>
{
    // ISO OID → "ImageRootIdentifierNamespace"
    private static readonly Guid _imageRootIdentifierNamespace = new("052d1dff-40bf-559f-b059-2b8bf0b32393");

    internal static Guid GetIDForSourceAndResourceID(DataSource imageSource, string resourceID)
        => UuidUtility.GetV5($"ImageSource={imageSource},ImageResourceID={resourceID}", _imageRootIdentifierNamespace);

    private PocoIndex<Guid, ShokoImage, int>? _localImageID;

    private PocoIndex<Guid, ShokoImage, Guid>? _primaryImageID;

    public ShokoImageRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginSaveCallback = obj =>
        {
            if (obj.LocalID == 0)
                obj.LocalID = GetAll().Select(x => x.LocalID).DefaultIfEmpty(0).Max() + 1;
        };
    }

    protected override Guid SelectKey(ShokoImage entity)
        => entity.ID;

    public override void PopulateIndexes()
    {
        _localImageID = Cache.CreateIndex(a => a.LocalID);
        _primaryImageID = Cache.CreateIndex(a => a.PrimaryID);
    }

    public ShokoImage? GetByLocalID(int localID)
        => ReadLock(() => _localImageID!.GetOne(localID));

    public IReadOnlyList<ShokoImage> GetByPrimaryImageID(Guid imageId)
        => ReadLock(() => _primaryImageID!.GetMultiple(imageId));

    public IReadOnlyList<ShokoImage> GetOrphanedImages(DateTime threshold)
        => GetAll()
            .Where(image => image.GetCrossReferences().Count is 0 && image.LastUpdatedAt < threshold)
            .ToList();
}
