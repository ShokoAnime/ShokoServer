using System;
using System.IO;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ImportFolderRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_ImportFolder, int>(databaseFactory)
{
    public event EventHandler? ImportFolderSaved;

    protected override int SelectKey(SVR_ImportFolder entity)
        => entity.ImportFolderID;

    public SVR_ImportFolder? GetByImportLocation(string importLocation)
    {
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.ImportFolderLocation?.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(
                    importLocation?.Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.InvariantCultureIgnoreCase) ?? false));
    }

    public SVR_ImportFolder SaveImportFolder(ImportFolder folder)
    {
        var ns = (folder.ImportFolderID > 0 ? GetByID(folder.ImportFolderID) : new()) ??
            throw new Exception($"Could not find Import Folder ID: {folder.ImportFolderID}");

        if (string.IsNullOrEmpty(folder.ImportFolderName))
            throw new Exception("Must specify an Import Folder name");

        if (string.IsNullOrEmpty(folder.ImportFolderLocation))
            throw new Exception("Must specify an Import Folder location");

        if (!Directory.Exists(folder.ImportFolderLocation))
            throw new Exception("Cannot find Import Folder location");

        if (GetAll()
            .ExceptBy([folder.ImportFolderID], iF => iF.ImportFolderID)
            .Any(f =>
                folder.ImportFolderLocation.StartsWith(f.ImportFolderLocation, StringComparison.OrdinalIgnoreCase) ||
                f.ImportFolderLocation.StartsWith(folder.ImportFolderLocation, StringComparison.OrdinalIgnoreCase)
            )
        )
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

    public (SVR_ImportFolder? folder, string relativePath) GetFromFullPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return default;

        var folders = GetAll();
        foreach (var folder in folders)
        {
            var importLocation = folder.ImportFolderLocation;
            var importLocationFull = importLocation.TrimEnd(Path.DirectorySeparatorChar);

            // add back the trailing backslashes
            importLocationFull += $"{Path.DirectorySeparatorChar}";

            importLocation = importLocation.TrimEnd(Path.DirectorySeparatorChar);
            if (fullPath.StartsWith(importLocationFull, StringComparison.InvariantCultureIgnoreCase))
            {
                var filePath = fullPath.Replace(importLocation, string.Empty);
                filePath = filePath.TrimStart(Path.DirectorySeparatorChar);
                return (folder, filePath);
            }
        }

        return default;
    }
}
