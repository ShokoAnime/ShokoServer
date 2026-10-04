using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Removes what the core stores for a series, film or collection that
///   nothing links to any more, on a source the core does not keep itself.
/// </summary>
/// <remarks>
///   Removes the entry from its store with everything stored under it, its
///   refresh time on its row included, removes a series' orderings and lets each
///   provider of the source clean up. Credited people stay until
///   the orphan purge. Holds the entry's locks throughout; unless forced, an entry
///   still linked (or a collection with a linked member) is left alone. Purging a
///   series or film queues the purge of each collection holding it.
/// </remarks>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Metadata)]
// Not default + 50: a prioritized cleanup only moves ahead of other cleanup.
[JobPriority(Default = 0, Prioritized = 10)]
public class PurgeMetadataJob(
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataSeriesStore seriesStore,
    IMetadataMovieStore movieStore,
    IMetadataCollectionStore collectionStore,
    MetadataEntityCleanup cleanup,
    MetadataOrderingService orderings,
    MetadataEntryLocks entryLocks,
    MetadataProviderScheduler providerScheduler,
    IJobCancellationAccessor cancellationAccessor,
    MetadataLinkChangeTracker? linkChanges = null
) : BaseJob
{
    #region Properties

    /// <summary>
    ///   The series, film or collection to purge, as its
    ///   <see cref="MetadataGuid"/> string.
    /// </summary>
    public string EntryID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether to purge the entry even though something still links to it,
    ///   removing those links first: the series and film links naming it and
    ///   the episode links pointing into a series. A collection is purged even
    ///   while one of its members is linked.
    /// </summary>
    public bool Force { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Purge Metadata";

    /// <inheritdoc />
    public override string Title => "Purging Metadata";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>().WithEntry(EntryID);
            if (Force)
                details["Force"] = true;
            return details;
        }
    }

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        if (!MetadataGuid.TryParse(EntryID, out var entry) || !providerScheduler.IsPurgeable(entry.Source))
        {
            _logger.LogWarning("Not purging {EntryID}, which is not an entry on a source the core purges.", EntryID);
            return;
        }

        var token = cancellationAccessor.Token;
        using var entryLock = await entryLocks.Acquire(entry, token).ConfigureAwait(false);
        using var imagesLock = await entryLocks.AcquireImages(entry, token).ConfigureAwait(false);
        using var updating = entryLocks.MarkUpdating(entry);
        if (Force)
            await RemoveLinksTo(entry, token).ConfigureAwait(false);

        var inUse = !Force && (entry.EntityType == MetadataEntityType.Collection
            ? crossReferences.IsCollectionInUse(collectionStore, entry)
            : crossReferences.IsLinked(entry));
        if (inUse)
        {
            _logger.LogInformation("Not purging {Entry}, which is still linked.", entry);
            return;
        }

        // Read before the entry goes, since its membership goes with it.
        var collections = entry.EntityType == MetadataEntityType.Collection
            ? []
            : collectionStore.GetCollectionsWith(entry).Select(collection => collection.ID).ToList();

        // The core's own sources keep their entries in tables of their own.
        if (!entry.Source.IsCore)
        {
            var removed = entry.EntityType switch
            {
                _ when entry.EntityType == MetadataEntityType.Series => seriesStore.RemoveSeries(entry),
                _ when entry.EntityType == MetadataEntityType.Movie => movieStore.RemoveMovie(entry),
                _ when entry.EntityType == MetadataEntityType.Collection => collectionStore.RemoveCollection(entry),
                _ => 0,
            };

            // A store removal takes the rest along; with nothing stored, what the
            // other stores may still hold for the entry is removed on its own.
            if (removed is 0)
                cleanup.Remove([entry]);
            _logger.LogInformation("Purged {Entry}: {Count} store rows.", entry, removed);
        }

        // Every ordering goes, whoever made it. A stored series lost them with its store rows
        // already; this covers series no longer stored.
        if (entry.EntityType == MetadataEntityType.Series)
            orderings.RemoveForSeries(entry);

        // Every provider claiming the source may keep something for the entry, disabled ones too.
        foreach (var info in providerManager.MetadataProviders.Where(provider => provider.Source == entry.Source))
        {
            try
            {
                await info.Provider.CleanUp(entry, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "{Provider} failed to clean up after {Entry} was purged.", info.Name, entry);
            }
        }

        // A collection left with no linked member goes too, in a purge under its own lock.
        foreach (var collection in collections)
        {
            try
            {
                await providerScheduler.SchedulePurge(collection, cancellationToken: token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to queue the purge of {Collection}, which held {Entry}.", collection, entry);
            }
        }
    }

    /// <summary>
    ///   Removes every link naming an entry: the series and film links naming
    ///   it, and for a series the episode links pointing into it and those
    ///   naming no series of an anime left with no series link.
    /// </summary>
    /// <param name="entry">The series or film; a collection has no links.</param>
    /// <param name="token">Cancels the work.</param>
    /// <returns>A task that completes once the links are gone.</returns>
    private async Task RemoveLinksTo(MetadataGuid entry, CancellationToken token)
    {
        var links = crossReferences.GetLinksTo(entry);
        var seriesLinks = links.OfType<IMetadataSeriesCrossReference>().ToList();
        var movieLinks = links.OfType<IMetadataMovieCrossReference>().ToList();
        var episodeLinks = crossReferences.GetEpisodeLinksInto(entry)
            .Concat(links.OfType<IMetadataEpisodeCrossReference>())
            .ToList();

        // An anime left with no series link loses its episode links naming
        // none too, as removing its last series link one at a time does.
        episodeLinks.AddRange(crossReferences.GetUnparentedEpisodeLinksLeftBy(
            entry,
            seriesLinks.Select(link => link.AnidbAnimeID).Concat(episodeLinks.Select(link => link.AnidbAnimeID))
        ));
        using var changes = linkChanges?.Begin(MetadataLinkChangeReason.Purge);
        if (seriesLinks.Count > 0)
            await crossReferences.MergeSeriesLinks([], seriesLinks, cancellationToken: token).ConfigureAwait(false);
        if (movieLinks.Count > 0)
            await crossReferences.MergeMovieLinks([], movieLinks, cancellationToken: token).ConfigureAwait(false);
        if (episodeLinks.Count > 0)
            await crossReferences.MergeEpisodeLinks([], episodeLinks, cancellationToken: token).ConfigureAwait(false);
        if (seriesLinks.Count + movieLinks.Count + episodeLinks.Count is 0)
            return;

        _logger.LogInformation("Removed {Count} links to {Entry} before purging it.", seriesLinks.Count + movieLinks.Count + episodeLinks.Count, entry);
    }

    #endregion
}
