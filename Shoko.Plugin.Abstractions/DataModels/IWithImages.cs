using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Container object with images.
/// </summary>
public interface IWithImages
{
    /// <summary>
    /// Get the preferred image for the given <paramref name="entityType"/> for
    /// the entity, or null if no preferred image is found.
    /// </summary>
    /// <param name="entityType">The entity type to search for.</param>
    /// <returns>The preferred image metadata for the given entity, or null if
    /// not found.</returns>
    IImageMetadata? GetPreferredImageForType(ImageEntityType entityType);

    /// <summary>
    /// Get all images for the entity, or all images for the given
    /// <paramref name="entityType"/> provided for the entity.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the entity.
    /// </returns>
    IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType = null);
}
