using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Exceptions;
using Shoko.Server.Models;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_PlaceRepository : BaseCachedRepository<SVR_VideoLocal_Place, int>
{
    private PocoIndex<int, SVR_VideoLocal_Place, int>? _videoLocalIDs;

    private PocoIndex<int, SVR_VideoLocal_Place, int>? _importFolderIDs;

    private PocoIndex<int, SVR_VideoLocal_Place, string>? _paths;

    public VideoLocal_PlaceRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginSaveCallback = place =>
        {
            if (place.VideoLocalID == 0)
                throw new InvalidStateException("Attempting to save a VideoLocal_Place with a VideoLocalID of 0");
            if (string.IsNullOrEmpty(place.FilePath))
                throw new InvalidStateException("Attempting to save a VideoLocal_Place with a null or empty FilePath");
            if (place.VideoLocal_Place_ID is 0 && GetByFilePathAndImportFolderID(place.FilePath, place.ImportFolderID) is { } secondPlace)
                throw new InvalidStateException("Attempting to save a VideoLocal_Place with a FilePath and ImportFolderID that already exists in the database");
        };
    }

    protected override int SelectKey(SVR_VideoLocal_Place entity)
        => entity.VideoLocal_Place_ID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.VideoLocalID);
        _importFolderIDs = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.ImportFolderID);
        _paths = new PocoIndex<int, SVR_VideoLocal_Place, string>(Cache, a => a.FilePath);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(VideoLocal_Place), " Removing orphaned VideoLocal_Places");
        var entries = Cache.Values.Where(a => a is { VideoLocalID: 0 } or { ImportFolderID: 0 } or { FilePath: null or "" }).ToList();
        var total = entries.Count;
        var current = 0;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        foreach (var batch in entries.Batch(50))
        {
            using var transaction = session.BeginTransaction();
            foreach (var entry in batch)
            {
                DeleteWithOpenTransaction(session, entry);
                current++;
                ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(VideoLocal_Place), " Removing Orphaned VideoLocal_Places - " + current + "/" + total);
            }

            transaction.Commit();
        }
    }

    public IReadOnlyList<SVR_VideoLocal_Place> GetByImportFolder(int importFolderID)
        => ReadLock(() => _importFolderIDs!.GetMultiple(importFolderID));

    public SVR_VideoLocal_Place? GetByFilePathAndImportFolderID(string filePath, int importFolderID)
        => !string.IsNullOrEmpty(filePath) && importFolderID > 0
            ? ReadLock(() => _paths!.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == importFolderID))
            : null;

    public IReadOnlyList<SVR_VideoLocal_Place> GetByVideoLocal(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));
}
