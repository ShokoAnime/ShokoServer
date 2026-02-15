using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoManagedFolderRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<ShokoManagedFolder, int>(databaseFactory)
{
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderAdded;

    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderUpdated;

    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderRemoved;

    protected override int SelectKey(ShokoManagedFolder entity)
        => entity.ID;

    public ShokoManagedFolder? GetByImportLocation(string importLocation)
    {
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.Path?.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(
                    importLocation?.Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.InvariantCultureIgnoreCase) ?? false));
    }

    public ShokoManagedFolder SaveFolder(ShokoManagedFolder folder)
    {
        var ns = (folder.ID > 0 ? GetByID(folder.ID) : new()) ??
            throw new Exception($"Could not find managed folder ID: {folder.ID}");

        if (string.IsNullOrEmpty(folder.Name))
            throw new Exception("Must specify an managed folder name");

        if (string.IsNullOrEmpty(folder.Path))
            throw new Exception("Must specify an managed folder location");

        if (!Directory.Exists(folder.Path))
            throw new Exception("Cannot find managed folder location");

        if (GetAll()
            .ExceptBy([folder.ID], iF => iF.ID)
            .Any(f =>
                folder.Path.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase) ||
                f.Path.StartsWith(folder.Path, StringComparison.OrdinalIgnoreCase)
            )
        )
            throw new Exception("Unable to nest an managed folder within another managed folder.");

        var isNew = folder.ID <= 0;
        var isUpdated = isNew;
        if (!string.Equals(ns.Name, folder.Name, StringComparison.Ordinal))
        {
            ns.Name = folder.Name;
            isUpdated = true;
        }

        if (!string.Equals(ns.Path, folder.Path, StringComparison.Ordinal))
        {
            ns.Path = folder.Path;
            isUpdated = true;
        }

        if (ns.IsDropDestination != folder.IsDropDestination)
        {
            ns.IsDropDestination = folder.IsDropDestination;
            isUpdated = true;
        }

        if (ns.IsDropSource != folder.IsDropSource)
        {
            ns.IsDropSource = folder.IsDropSource;
            isUpdated = true;
        }

        if (ns.IsWatched != folder.IsWatched)
        {
            ns.IsWatched = folder.IsWatched;
            isUpdated = true;
        }

        base.Save(ns);

        // Only fire the events if something changed or if it's a new folder.
        if (isNew || isUpdated)
            Task.Run(() => DispatchEvent(folder, isNew));

        return ns;
    }

    private void DispatchEvent(ShokoManagedFolder folder, bool isNew)
    {
        if (isNew)
            ManagedFolderAdded?.Invoke(null, new() { Folder = folder });
        else
            ManagedFolderUpdated?.Invoke(null, new() { Folder = folder });
    }

    public (ShokoManagedFolder? folder, string? relativePath) GetFromAbsolutePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return default;

        path = PlatformUtility.NormalizePath(path, platformFormat: true);
        if (!Path.IsPathFullyQualified(path))
            return default;

        var folders = GetAll();
        foreach (var folder in folders)
        {
            var folderPath = folder.Path;
            if (string.Equals(path[..^1], folderPath, PlatformUtility.StringComparison) || path.StartsWith(folderPath, PlatformUtility.StringComparison))
            {
                var filePath = PlatformUtility.NormalizePath(path[folderPath.Length..], stripLeadingSlash: true);
                if (filePath is "")
                    filePath = null;
                return (folder, filePath);
            }
        }

        return default;
    }

    public override void Delete(ShokoManagedFolder folder)
    {
        base.Delete(folder);

        Task.Run(() => ManagedFolderRemoved?.Invoke(null, new() { Folder = folder }));
    }
}
