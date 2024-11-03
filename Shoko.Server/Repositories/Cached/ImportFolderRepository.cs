using System;
using System.IO;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class ImportFolderRepository : BaseCachedRepository<SVR_ImportFolder, int>
{
    public EventHandler ImportFolderSaved;

    public ImportFolderRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }

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

        if (GetAll().ExceptBy([folder.ImportFolderID], iF => iF.ImportFolderID).Any(iF =>
        {
            var comparison = Utils.GetComparisonFor(folder.ImportFolderLocation, iF.ImportFolderLocation);
            var newLocation = folder.ImportFolderLocation.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (newLocation[^1] != Path.DirectorySeparatorChar)
                newLocation += Path.DirectorySeparatorChar;
            var existingLocation = iF.ImportFolderLocation.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (existingLocation[^1] != Path.DirectorySeparatorChar)
                existingLocation += Path.DirectorySeparatorChar;
            return newLocation.StartsWith(existingLocation, comparison) || existingLocation.StartsWith(newLocation, comparison);
        }))
            throw new Exception("Unable to nest an import folder within another import folder.");

        ns.ImportFolderName = folder.ImportFolderName;
        ns.ImportFolderLocation = folder.ImportFolderLocation;
        ns.IsDropDestination = folder.IsDropDestination;
        ns.IsDropSource = folder.IsDropSource;
        ns.IsWatched = folder.IsWatched;
        ns.ImportFolderType = folder.ImportFolderType;

        Save(ns);

        ImportFolderSaved?.Invoke(null, EventArgs.Empty);

        return ns;
    }

    public (SVR_ImportFolder folder, string relativePath) GetFromFullPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return default;
        var shares = GetAll();

        // TODO make sure that import folders are not sub folders of each other
        foreach (var ifolder in shares)
        {
            var importLocation = ifolder.ImportFolderLocation;
            var importLocationFull = importLocation.TrimEnd(Path.DirectorySeparatorChar);

            // add back the trailing backslashes
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
}
