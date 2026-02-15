using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Databases;
using Shoko.Server.Exceptions;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_PlaceRepository : BaseCachedRepository<VideoLocal_Place, int>
{
    private PocoIndex<int, VideoLocal_Place, int>? _videoLocalIDs;

    private PocoIndex<int, VideoLocal_Place, int>? _managedFolderIDs;

    private PocoIndex<int, VideoLocal_Place, string>? _paths;

    public VideoLocal_PlaceRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginSaveCallback = place =>
        {
            if (place.VideoID == 0)
                throw new InvalidStateException("Attempting to save a VideoLocal_Place with a VideoLocalID of 0");
            if (string.IsNullOrEmpty(place.RelativePath))
                throw new InvalidStateException("Attempting to save a VideoLocal_Place with a null or empty FilePath");
            if (place.ID is 0 && GetByRelativePathAndManagedFolderID(place.RelativePath, place.ManagedFolderID) is { } secondPlace)
                throw new InvalidStateException("Attempting to save a VideoLocal_Place with a FilePath and ManagedFolderID that already exists in the database");
        };
    }

    protected override int SelectKey(VideoLocal_Place entity)
        => entity.ID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = Cache.CreateIndex(a => a.VideoID);
        _managedFolderIDs = Cache.CreateIndex(a => a.ManagedFolderID);
        _paths = Cache.CreateIndex(a => a.RelativePath);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(VideoLocal_Place)} Removing orphaned VideoLocal_Places...";
        var entries = Cache.Values.Where(a => a is { VideoID: 0 } or { ManagedFolderID: 0 } or { RelativePath: null or "" }).ToList();
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
                ServerState.Instance.ServerStartingStatus =
                    $"Database - Validating - {nameof(VideoLocal_Place)} Removing Orphaned VideoLocal_Places - {current}/{total}...";
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Gets the <see cref="VideoLocal_Place"/> associated with the given file path, but only if it is unique across all managed folders.
    /// </summary>
    /// <param name="relativePath">The file path to search for.</param>
    /// <returns>The associated <see cref="VideoLocal_Place"/>, or null if not found.</returns>
    /// <exception cref="InvalidStateException">When the given file path is null or empty.</exception>
    public VideoLocal_Place? GetByRelativePath(string relativePath)
        => !string.IsNullOrEmpty(relativePath)
            ? ReadLock(() => _paths!.GetMultiple(PlatformUtility.NormalizePath(relativePath, stripLeadingSlash: true)) is { Count: 1 } list ? list[0] : null)
            : null;

    public IReadOnlyList<VideoLocal_Place> GetByManagedFolderID(int managedFolderID)
        => ReadLock(() => _managedFolderIDs!.GetMultiple(managedFolderID));

    /// <summary>
    /// Gets the <see cref="VideoLocal_Place"/> associated with the given file path and managed folder ID.
    /// </summary>
    /// <param name="relativePath">The file path to search for.</param>
    /// <param name="managedFolderID">The managed folder ID to search within.</param>
    /// <returns>The associated <see cref="VideoLocal_Place"/>, or null if not found.</returns>
    /// <exception cref="InvalidStateException">When the given file path is null or empty, or the given managed folder ID is 0.</exception>
    public VideoLocal_Place? GetByRelativePathAndManagedFolderID(string relativePath, int managedFolderID)
        => !string.IsNullOrEmpty(relativePath) && managedFolderID > 0
            ? ReadLock(() => _paths!.GetMultiple(PlatformUtility.NormalizePath(relativePath, stripLeadingSlash: true)).FirstOrDefault(a => a.ManagedFolderID == managedFolderID))
            : null;

    public IReadOnlyList<VideoLocal_Place> GetByVideoLocal(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));
}
