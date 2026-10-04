using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Services;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// The source-wide actions of the <c>Metadata/{source}/Action</c> routes,
/// which the old TMDB action routes run too, so both answer the same way.
/// </summary>
/// <remarks>
/// Each action can run for a long time over a large library, so the routes
/// start it in the background with <see cref="Start"/> and answer at once.
/// </remarks>
/// <param name="refreshService">Refreshes entries, downloads images and runs auto-searches.</param>
/// <param name="purgeService">Purges entries.</param>
/// <param name="linkingService">Removes links.</param>
/// <param name="imageManager">Purges images.</param>
/// <param name="logger">Logs what an action did, and why it failed.</param>
public sealed class MetadataSourceActions(
    IMetadataRefreshService refreshService,
    IMetadataPurgeService purgeService,
    IMetadataLinkingService linkingService,
    IImageManager imageManager,
    ILogger<MetadataSourceActions> logger
)
{
    #region Sources

    /// <summary>
    /// Whether anything can be linked to a source: every source but AniDB,
    /// the hub everything is linked from, and the server's own.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><c>true</c> for a source entries are linked to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public static bool IsLinkTarget(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source != MetadataSource.AniDB && source != MetadataSource.Shoko && source != MetadataSource.User && source != MetadataSource.Generated;
    }

    #endregion

    #region Running

    /// <summary>
    /// Starts an action in the background, logging its outcome.
    /// </summary>
    /// <param name="description">What the action is, for the log.</param>
    /// <param name="action">The action.</param>
    /// <returns>The running action, which never throws.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> or <paramref name="action"/> is <c>null</c>.</exception>
    public Task Start(string description, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(action);

        // Started from a request that returns at once, so it carries the caller's actor itself.
        return Task.Run(ActorContext.Carry(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Action} failed.", description);
            }
        }));
    }

    #endregion

    #region Refresh

    /// <summary>
    /// Refreshes every entry of a source that is linked to an anime.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">Only the series or only the movies, or <c>null</c> for both.</param>
    /// <param name="force">Whether to refresh entries however recently they were.</param>
    /// <param name="downloadImages">Whether to download the images too.</param>
    /// <param name="cancellationToken">Cancels the queueing.</param>
    /// <returns>How many refreshes were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> RefreshAllLinked(MetadataSource source, MetadataEntityType? entityType, bool force, bool downloadImages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var options = new MetadataRefreshOptions { DownloadImages = downloadImages, Reason = MetadataRefreshReason.Requested };
        var count = await refreshService.RefreshAllLinked(source, force, options, entityType, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Queued {Count} {Source} refreshes of linked {Kind}.", count, source.Name, entityType?.Value ?? "entries");
        return count;
    }

    /// <summary>
    /// Downloads the wanted images of every stored entry of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="force">Whether to download images that are there already.</param>
    /// <param name="cancellationToken">Cancels the queueing.</param>
    /// <returns>How many downloads were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> DownloadAllImages(MetadataSource source, bool force, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = await refreshService.DownloadAllImages(source, force, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Queued {Count} {Source} image downloads.", count, source.Name);
        return count;
    }

    /// <summary>
    /// Searches for a match for every anime the source is not linked on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="force">Whether to search the anime left alone too, and while the source does not auto-link; linked anime are never searched.</param>
    /// <param name="cancellationToken">Cancels the queueing.</param>
    /// <returns>How many searches were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> AutoSearchAll(MetadataSource source, bool force, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = await refreshService.AutoSearchAll(source, force, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Queued {Count} {Source} auto-searches.", count, source.Name);
        return count;
    }

    #endregion

    #region Purge

    /// <summary>
    /// Purges the stored series or movies of a source that nothing links to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">Only the series or only the movies, or <c>null</c> for both.</param>
    /// <param name="olderThan">Only the ones last refreshed before this, or <c>null</c> for all.</param>
    /// <param name="cancellationToken">Cancels the queueing.</param>
    /// <returns>How many purges were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> PurgeUnused(MetadataSource source, MetadataEntityType? entityType, DateTime? olderThan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = await purgeService.PurgeUnused(source, olderThan, entityType, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Queued {Count} {Source} purges of unused {Kind}.", count, source.Name, entityType?.Value ?? "entries");
        return count;
    }

    /// <summary>
    /// Purges the stored collections of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>How many collections were purged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> PurgeCollections(MetadataSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = await purgeService.PurgeCollections(source, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Purged {Count} {Source} collections.", count, source.Name);
        return count;
    }

    /// <summary>
    /// Purges what a source keeps that no stored entry uses any more, such
    /// as people, tags and studios.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="orphanedBefore">Only what was orphaned before this, or <c>null</c> for all.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>How many orphans were purged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> PurgeOrphaned(MetadataSource source, DateTime? orphanedBefore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = await purgeService.PurgeOrphaned(source, orphanedBefore, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Purged {Count} orphaned {Source} entries.", count, source.Name);
        return count;
    }

    /// <summary>
    /// Queues the purge of a source's images that nothing links to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A task that completes once the purge is queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task PurgeUnusedImages(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        await imageManager.SchedulePurgeOfOrphanedImages(0, source).ConfigureAwait(false);
        logger.LogInformation("Queued the purge of unused {Source} images.", source.Name);
    }

    #endregion

    #region Links

    /// <summary>
    /// Removes every link of a source, and sets whether it may auto-link the
    /// anime again.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="removeSeriesLinks">Whether to remove the series links, with the episode links.</param>
    /// <param name="removeMovieLinks">Whether to remove the movie links.</param>
    /// <param name="purge">Whether to purge what the links pointed at.</param>
    /// <param name="resetAutoLinkingState">
    /// <c>false</c> to let the source auto-link every anime again,
    /// <c>true</c> to keep it from auto-linking any, or
    /// <c>null</c> to leave that as it is.
    /// </param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<int> RemoveAllLinks(
        MetadataSource source,
        bool removeSeriesLinks,
        bool removeMovieLinks,
        bool purge,
        bool? resetAutoLinkingState,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = removeSeriesLinks || removeMovieLinks
            ? await linkingService.RemoveAllLinks(
                source,
                removeSeriesLinks,
                removeMovieLinks,
                purge,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false)
            : 0;
        if (resetAutoLinkingState is { } disabled)
            linkingService.ResetAutoLinkingState(source, disabled);

        logger.LogInformation("Removed {Count} {Source} links.", count, source.Name);
        return count;
    }

    #endregion
}
