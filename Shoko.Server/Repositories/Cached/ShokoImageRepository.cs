using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class ShokoImageRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<ShokoImage, Guid>(databaseFactory)
{
    private int _lastLocalID;

    private PocoIndex<Guid, ShokoImage, int>? _localImageID;

    // Only the images linked to another primary image, which few are.
    private PocoIndex<Guid, ShokoImage, Guid>? _linkedPrimaryImageID;

    protected override void OnBeginSave(ShokoImage obj)
    {
        if (obj.LocalID == 0)
            obj.LocalID = Interlocked.Increment(ref _lastLocalID);
    }

    protected override Guid SelectKey(ShokoImage entity)
        => entity.ID;

    public override void PopulateIndexes()
    {
        _lastLocalID = Cache.GetAll().Select(a => a.LocalID).DefaultIfEmpty(0).Max();
        _localImageID = Cache.CreateIndex(a => a.LocalID);
        _linkedPrimaryImageID = Cache.CreateIndex(a => a.PrimaryID == a.ID ? [] : (IReadOnlyList<Guid>)[a.PrimaryID]);
    }

    public ShokoImage? GetByLocalID(int localID)
        => _localImageID!.GetOne(localID);

    public IReadOnlyList<ShokoImage> GetByPrimaryImageID(Guid imageId)
    {
        var images = _linkedPrimaryImageID!.GetMultiple(imageId);
        if (GetByIDUnsafe(imageId) is not { } primaryImage || primaryImage.PrimaryID != imageId)
            return images;

        return [primaryImage, .. images];
    }

    public IReadOnlyList<ShokoImage> GetOrphanedImages(DateTime threshold)
        => GetAll()
            .Where(image => image.GetCrossReferences().Count is 0 && image.LastUpdatedAt < threshold)
            .ToList();
}
