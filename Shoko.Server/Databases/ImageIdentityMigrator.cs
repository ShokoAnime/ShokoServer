using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Databases;

/// <summary>
/// Works out the image IDs hashed from source values that replace the IDs
/// older versions hashed from the old enum spelling, and what has to change
/// in the stored rows to get there. Holds no state, so each plan can be made
/// again from whatever an interrupted run left behind.
/// </summary>
internal static class ImageIdentityMigrator
{
    #region IDs

    /// <summary>
    /// Gets the ID an image has now, hashed from its source's value.
    /// </summary>
    /// <param name="source">The image source.</param>
    /// <param name="resourceID">The resource ID within the source.</param>
    /// <returns>The new image ID.</returns>
    public static Guid GetNewID(MetadataSource source, string resourceID)
        => IImageManager.GetIDForImageSourceAndResourceID(source, resourceID);

    /// <summary>
    /// Builds the map from every stored image ID to its new ID.
    /// </summary>
    /// <param name="images">Every stored image.</param>
    /// <returns>The map from stored IDs to new IDs.</returns>
    public static IReadOnlyDictionary<Guid, Guid> BuildIDMap(IEnumerable<ImageIdentity> images)
    {
        var map = new Dictionary<Guid, Guid>();
        foreach (var image in images)
            map.TryAdd(image.ID, image.NewID);

        return map;
    }

    /// <summary>
    /// Maps an image ID to its new ID.
    /// </summary>
    /// <param name="map">The map from <see cref="BuildIDMap"/>.</param>
    /// <param name="id">The ID to map.</param>
    /// <returns>The new ID, or <paramref name="id"/> when it is not in the map.</returns>
    public static Guid MapID(IReadOnlyDictionary<Guid, Guid> map, Guid id)
        => map.TryGetValue(id, out var newID) ? newID : id;

    /// <summary>
    /// Gets every ID an image's file may be named after: its new ID and its
    /// stored ID, in that order and without repeats.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns>The IDs, as 32 hex digits.</returns>
    public static IReadOnlyList<string> GetFileIDs(ImageIdentity image)
        => new[] { image.NewID, image.ID }
            .Distinct()
            .Select(id => id.ToString("N"))
            .ToList();

    #endregion

    #region Plans

    /// <summary>
    /// Plans the primary image IDs to rewrite: every image whose primary ID
    /// points at another image and maps to another ID. An image that is its
    /// own primary image gets its new primary ID when its row is renamed, as
    /// <see cref="GetPrimaryIDAfterRename"/> gives it.
    /// </summary>
    /// <param name="images">Every stored image.</param>
    /// <param name="map">The map from <see cref="BuildIDMap"/>.</param>
    /// <returns>The stored ID and new primary ID of each image to change.</returns>
    public static IReadOnlyList<(Guid ID, Guid PrimaryID)> PlanPrimaryIDs(IEnumerable<ImageIdentity> images, IReadOnlyDictionary<Guid, Guid> map)
        => images
            .Where(image => image.PrimaryID != image.ID)
            .Select(image => (image.ID, PrimaryID: MapID(map, image.PrimaryID), OldPrimaryID: image.PrimaryID))
            .Where(tuple => tuple.PrimaryID != tuple.OldPrimaryID)
            .Select(tuple => (tuple.ID, tuple.PrimaryID))
            .ToList();

    /// <summary>
    /// Gets the primary ID a renamed row gets: its new ID if it was its own
    /// primary image, or else the primary ID it holds, which
    /// <see cref="PlanPrimaryIDs"/> already rewrote.
    /// </summary>
    /// <param name="id">The stored ID.</param>
    /// <param name="primaryID">The stored primary ID.</param>
    /// <param name="newID">The ID the row gets.</param>
    /// <returns>The primary ID for the renamed row.</returns>
    public static Guid GetPrimaryIDAfterRename(Guid id, Guid primaryID, Guid newID)
        => primaryID == id ? newID : primaryID;

    /// <summary>
    /// Plans the stored image rows to rename: every row whose stored ID is
    /// not its new ID.
    /// </summary>
    /// <param name="images">Every stored image.</param>
    /// <returns>The rows to rename, and nothing for rows already right.</returns>
    public static IReadOnlyList<ImageIDChange> PlanRowChanges(IEnumerable<ImageIdentity> images)
        => images
            .Where(image => image.ID != image.NewID)
            .Select(image => new ImageIDChange(image.ID, image.NewID))
            .ToList();

    #endregion
}

/// <summary>
/// What the image ID migration reads from a stored image.
/// </summary>
/// <param name="ID">The stored ID.</param>
/// <param name="PrimaryID">The stored primary image ID.</param>
/// <param name="Source">The image source.</param>
/// <param name="ResourceID">The resource ID within the source.</param>
internal sealed record ImageIdentity(Guid ID, Guid PrimaryID, MetadataSource Source, string ResourceID)
{
    /// <summary>
    /// The ID hashed from the source's value.
    /// </summary>
    public Guid NewID { get; } = ImageIdentityMigrator.GetNewID(Source, ResourceID);
}

/// <summary>
/// A stored image row to rename.
/// </summary>
/// <param name="ID">The stored ID.</param>
/// <param name="NewID">The ID the row gets.</param>
internal sealed record ImageIDChange(Guid ID, Guid NewID);
