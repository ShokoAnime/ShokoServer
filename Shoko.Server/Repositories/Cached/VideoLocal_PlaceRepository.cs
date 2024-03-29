using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Exceptions;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_PlaceRepository : BaseCachedRepository<SVR_VideoLocal_Place, int>
{
    private PocoIndex<int, SVR_VideoLocal_Place, int> VideoLocals;
    private PocoIndex<int, SVR_VideoLocal_Place, int> ImportFolders;
    private PocoIndex<int, SVR_VideoLocal_Place, string> Paths;

    public VideoLocal_PlaceRepository()
    {
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        BeginSaveCallback = place =>
        {
            if (place.VideoLocalID == 0) throw new InvalidStateException("Attempting to save a VideoLocal_Place with a VideoLocalID of 0");
        };
    }

    protected override int SelectKey(SVR_VideoLocal_Place entity)
    {
        return entity.VideoLocal_Place_ID;
    }

    public override void PopulateIndexes()
    {
        VideoLocals = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.VideoLocalID);
        ImportFolders = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.ImportFolderID);
        Paths = new PocoIndex<int, SVR_VideoLocal_Place, string>(Cache, a => a.FilePath);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(VideoLocal_Place), " Removing orphaned VideoLocal_Places");
        var count = 0;
        int max;

        var list = Cache.Values.Where(a => a is { VideoLocalID: 0 }).ToList();
        max = list.Count;

        using var session = DatabaseFactory.SessionFactory.OpenSession();
        foreach (var batch in list.Batch(50))
        {
            using var transaction = session.BeginTransaction();
            foreach (var a in batch)
            {
                DeleteWithOpenTransaction(session, a);
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(VideoLocal_Place),
                    " Removing Orphaned VideoLocal_Places - " + count + "/" + max);
            }

            transaction.Commit();
        }
    }

    public List<SVR_VideoLocal_Place> GetByImportFolder(int importFolderID)
    {
        if (importFolderID == 0) throw new InvalidStateException("Trying to lookup a VideoLocal_Place by an ImportFolderID of 0");
        return ReadLock(() => ImportFolders.GetMultiple(importFolderID));
    }

    public SVR_VideoLocal_Place GetByFilePathAndImportFolderID(string filePath, int importFolderID)
    {
        if (string.IsNullOrEmpty(filePath)) throw new InvalidStateException("Trying to lookup a VideoLocal_Place by an empty File Path");
        if (importFolderID == 0) throw new InvalidStateException("Trying to lookup a VideoLocal_Place by an ImportFolderID of 0");
        return ReadLock(() => Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == importFolderID));
    }

    public static (SVR_ImportFolder folder, string relativePath) GetFromFullPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return default;
        var shares = RepoFactory.ImportFolder.GetAll();

        // TODO make sure that import folders are not sub folders of each other
        foreach (var ifolder in shares)
        {
            var importLocation = ifolder.ImportFolderLocation;
            var importLocationFull = importLocation.TrimEnd(Path.DirectorySeparatorChar);

            // add back the trailing back slashes
            importLocationFull += $"{Path.DirectorySeparatorChar}";

            importLocation = importLocation.TrimEnd(Path.DirectorySeparatorChar);
            if (fullPath.StartsWith(importLocationFull, StringComparison.InvariantCultureIgnoreCase))
            {
                var filePath = fullPath.Replace(importLocation, string.Empty);
                filePath = filePath.TrimStart(Path.DirectorySeparatorChar);
                return (ifolder, filePath);
            }
        }

        return default;
    }

    public List<SVR_VideoLocal_Place> GetByVideoLocal(int videoLocalID)
    {
        if (videoLocalID == 0) throw new InvalidStateException("Trying to lookup a VideoLocal_Place by a VideoLocalID of 0");
        return ReadLock(() => VideoLocals.GetMultiple(videoLocalID));
    }
}
