﻿using System;
using System.IO;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class ImportFolderRepository : BaseCachedRepository<SVR_ImportFolder, int>
{
    protected override int SelectKey(SVR_ImportFolder entity)
    {
        return entity.ImportFolderID;
    }

    public override void PopulateIndexes()
    {
    }

    public override void RegenerateDb()
    {
    }

    public SVR_ImportFolder GetByImportLocation(string importloc)
    {
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.ImportFolderLocation?.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(
                    importloc?.Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.InvariantCultureIgnoreCase) ?? false));
    }

    public SVR_ImportFolder SaveImportFolder(ImportFolder folder)
    {
        SVR_ImportFolder ns;
        if (folder.ImportFolderID > 0)
        {
            // update
            ns = GetByID(folder.ImportFolderID);
            if (ns == null)
            {
                throw new Exception($"Could not find Import Folder ID: {folder.ImportFolderID}");
            }
        }
        else
        {
            // create
            ns = new SVR_ImportFolder();
        }

        if (string.IsNullOrEmpty(folder.ImportFolderName))
        {
            throw new Exception("Must specify an Import Folder name");
        }

        if (string.IsNullOrEmpty(folder.ImportFolderLocation))
        {
            throw new Exception("Must specify an Import Folder location");
        }

        if (!Directory.Exists(folder.ImportFolderLocation))
        {
            throw new Exception("Cannot find Import Folder location");
        }

        if (folder.ImportFolderID == 0)
        {
            var nsTemp =
                GetByImportLocation(folder.ImportFolderLocation);
            if (nsTemp != null)
            {
                throw new Exception("Another entry already exists for the specified Import Folder location");
            }
        }

        ns.ImportFolderName = folder.ImportFolderName;
        ns.ImportFolderLocation = folder.ImportFolderLocation;
        ns.IsDropDestination = folder.IsDropDestination;
        ns.IsDropSource = folder.IsDropSource;
        ns.IsWatched = folder.IsWatched;
        ns.ImportFolderType = folder.ImportFolderType;

        Save(ns);

        Utils.MainThreadDispatch(() => { ServerInfo.Instance.RefreshImportFolders(); });
        ShokoServer.StopWatchingFiles();
        ShokoServer.StartWatchingFiles();

        return ns;
    }
}
