using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Image;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Supplies the images your source has for its entities.
/// </summary>
/// <remarks>
///   Optional. The core's image job for you walks a linked entry's entities
///   (seasons, episodes, credited people and studios included), asks you for
///   each one's images, and links and downloads them by the admin's image
///   settings for your source. Register your source's template URL with
///   <see cref="Services.IImageManager.RegisterTemplateUrl"/> first, since
///   every <see cref="ImageCandidate.ResourceID"/> completes it.
/// </remarks>
public interface IMetadataImageProvider : IMetadataProvider
{
    /// <summary>
    ///   The images your source has for one of its entities.
    /// </summary>
    /// <remarks>
    ///   Return every image you have, in your source's own order, not only
    ///   the ones to download; the core picks those. An image left out that
    ///   was linked before is unlinked from the entity.
    /// </remarks>
    /// <param name="entityID">
    ///   The entity: on your source, and a series, season, episode, movie,
    ///   collection, creator, character or studio.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The images, or <see langword="null"/> when you have nothing to say
    ///   about the entity, which leaves its linked images alone.
    /// </returns>
    Task<IReadOnlyList<ImageCandidate>?> GetImages(MetadataGuid entityID, CancellationToken cancellationToken = default);
}
