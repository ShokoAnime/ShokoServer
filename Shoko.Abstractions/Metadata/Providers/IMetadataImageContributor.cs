using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Image;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Adds images from a source of your own to entries of other sources, such
///   as artwork for TMDB's shows and movies from an image-only service.
/// </summary>
/// <remarks>
///   Whenever the core refreshes an entry's images, it queues a job for each
///   contributor whose enabled pairs cover the entry or anything under it; the
///   job asks you for each such entity's images and links and downloads them
///   under your <see cref="Source"/>, by the admin's image settings for it.
///   Register your source first. A remote source's images are fetched through
///   its template URL (<see cref="Services.IImageManager.RegisterTemplateUrl"/>),
///   and a local source's images through <see cref="GetImageStream"/>.
/// </remarks>
public interface IMetadataImageContributor
{
    /// <summary>
    ///   The display name, typically matching the plugin's.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Describes what the contributor adds.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   The contributor's version, defaulting to the assembly's.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   The source your images and their links are kept under.
    /// </summary>
    /// <remarks>
    ///   The instance <see cref="MetadataSource.Register"/> returned, read once
    ///   at registration. An unregistered source, a core one, or one another
    ///   contributor already uses, is refused. A local source serves its
    ///   images through <see cref="GetImageStream"/>.
    /// </remarks>
    MetadataSource Source { get; }

    /// <summary>
    ///   The sources and kinds of the entities you add images for. Core
    ///   sources are welcome.
    /// </summary>
    /// <remarks>
    ///   Read once, at registration. A pair on your own <see cref="Source"/>
    ///   is dropped, since your provider's image job serves those. Every pair
    ///   starts enabled, and an admin can turn each one off.
    /// </remarks>
    MetadataEntityScope Scope { get; }

    /// <summary>
    ///   How many of your image jobs may run at once, or
    ///   <c>null</c> for the core's default of two.
    /// </summary>
    /// <remarks>
    ///   Your jobs run as a job type of your own, with a pool of this size.
    ///   Read once, at registration.
    /// </remarks>
    int? MaxConcurrentJobs { get => null; }

    /// <summary>
    ///   The embedded resource of your contributor's icon, shown beside its
    ///   name. Must be an absolute resource name, including the assembly name.
    /// </summary>
    /// <remarks>
    ///   Any format the image system takes is accepted, SVG staying sharpest;
    ///   make it square and readable at 16
    ///   pixels. It is extracted beside your plugin as
    ///   <c>&lt;source&gt;.images-icon.&lt;ext&gt;</c>, or
    ///   <c>&lt;dll&gt;.&lt;source&gt;.images-icon.&lt;ext&gt;</c> beside a
    ///   lone dll, named after your <see cref="Source"/>, and a file already
    ///   there by that name is used instead. Read once, at registration.
    /// </remarks>
    /// <example>
    ///   <c>"Shoko.Plugin.Example.assets.artwork-icon.svg"</c>, or the same
    ///   name as <see cref="Plugin.IPlugin.EmbeddedIconResourceName"/> to
    ///   reuse the plugin's icon.
    /// </example>
    string? EmbeddedIconResourceName { get => null; }

    /// <summary>
    ///   The images you have for one entity.
    /// </summary>
    /// <remarks>
    ///   Return every image you have, in your own order, not only the ones to
    ///   download; the core picks those. An image left out that you linked
    ///   before is unlinked from the entity. Narrow the entity down with a
    ///   type check, such as <c>entity is IMovie movie</c>, and read its
    ///   cross-source IDs to find it on your service.
    /// </remarks>
    /// <param name="entity">
    ///   The entity, of a pair enabled for you, as the core resolves it.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The images, or <c>null</c> when you have nothing to say
    ///   about the entity, which leaves your linked images alone.
    /// </returns>
    Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///   The contents of one of your images, when your <see cref="Source"/> is
    ///   local. Never called for a remote source, whose images are fetched
    ///   through its template URL.
    /// </summary>
    /// <param name="resourceID">The image's resource ID, as your candidate gave it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   A stream the core reads and disposes, or <c>null</c> when the image
    ///   is gone.
    /// </returns>
    Task<Stream?> GetImageStream(string resourceID, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(null);

    /// <summary>
    ///   Whether one of your images can still be had, when your
    ///   <see cref="Source"/> is local. Override it when that is cheaper to
    ///   tell than producing the image.
    /// </summary>
    /// <param name="resourceID">The image's resource ID, as your candidate gave it.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns><c>true</c> when <see cref="GetImageStream"/> would return the image.</returns>
    async Task<bool> CheckIfImageAvailable(string resourceID, CancellationToken cancellationToken = default)
    {
        await using var stream = await GetImageStream(resourceID, cancellationToken).ConfigureAwait(false);
        return stream is not null;
    }
}
