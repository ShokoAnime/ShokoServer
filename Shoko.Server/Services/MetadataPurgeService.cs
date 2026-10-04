using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Purges a source's unused entries through the core's purge job, and its
///   orphaned people, studios and networks and its series' leftovers at once.
/// </summary>
/// <param name="crossReferences">The links, which decide what is unused.</param>
/// <param name="metadataService">Every source's stored series, films and collections.</param>
/// <param name="seriesStore">Removes what is left of a series with no row of its own.</param>
/// <param name="collectionStore">The stored collections' members.</param>
/// <param name="peopleStore">The stored creators and characters.</param>
/// <param name="studioStore">The stored studios and networks.</param>
/// <param name="tagStore">The stored tags, whose unused ones go with the orphans.</param>
/// <param name="creators">The creators' table, to find the sources that have people.</param>
/// <param name="characters">The characters' table, to find the sources that have people.</param>
/// <param name="studios">The studios' table, to find the sources that have studios.</param>
/// <param name="networks">The networks' table, to find the sources that have networks.</param>
/// <param name="tags">The tags' table, to find the sources that have tags.</param>
/// <param name="seriesRows">The stored series, to find the seasons and episodes left without one.</param>
/// <param name="seasonRows">The stored seasons.</param>
/// <param name="episodeRows">The stored episodes.</param>
/// <param name="cleanup">Unlinks the images of the people, studios and networks removed.</param>
/// <param name="entryLocks">Keeps a refresh and a purge of the same series apart.</param>
/// <param name="providerScheduler">Queues the purges.</param>
/// <param name="settingsProvider">Holds how long a person, studio or network may stay orphaned.</param>
/// <param name="logger">Where the purges are reported.</param>
public class MetadataPurgeService(
    IMetadataCrossReferenceStore crossReferences,
    IMetadataService metadataService,
    IMetadataSeriesStore seriesStore,
    IMetadataCollectionStore collectionStore,
    IMetadataPeopleStore peopleStore,
    IMetadataStudioStore studioStore,
    MetadataTagStore tagStore,
    Metadata_CreatorRepository creators,
    Metadata_CharacterRepository characters,
    Metadata_StudioRepository studios,
    Metadata_NetworkRepository networks,
    Metadata_TagRepository tags,
    Metadata_SeriesRepository seriesRows,
    Metadata_SeasonRepository seasonRows,
    Metadata_EpisodeRepository episodeRows,
    MetadataEntityCleanup cleanup,
    MetadataEntryLocks entryLocks,
    MetadataProviderScheduler providerScheduler,
    ISettingsProvider settingsProvider,
    ILogger<MetadataPurgeService> logger
) : IMetadataPurgeService
{
    #region Entries

    /// <inheritdoc />
    public Task<bool> PurgeEntry(MetadataGuid entryID, bool force = false, CancellationToken cancellationToken = default)
        => providerScheduler.SchedulePurge(entryID, force, cancellationToken);

    /// <inheritdoc />
    public async Task<int> PurgeUnused(
        MetadataSource source,
        DateTime? olderThan = null,
        MetadataEntityType? entityType = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!providerScheduler.IsPurgeable(source))
            return 0;

        // A collection of a source the core keeps in its own tables goes with
        // the last film it holds, and its members are not in the store.
        bool Wanted(MetadataEntityType kind) => entityType is null || entityType == kind;
        var unused = (Wanted(MetadataEntityType.Series) ? metadataService.GetAllSeriesForSource(source).Cast<IMetadata>() : [])
            .Concat(Wanted(MetadataEntityType.Movie) ? metadataService.GetAllMoviesForSource(source) : [])
            .Where(entry => !crossReferences.IsLinked(entry.ID))
            .Concat(Wanted(MetadataEntityType.Collection) && !source.IsCore
                ? collectionStore.GetAllCollections(source).Where(collection => !crossReferences.IsCollectionInUse(collectionStore, collection.ID))
                : [])
            .Where(entry => olderThan is not { } cutoff || GetLastTouchedAt(entry) is not { } touchedAt || touchedAt < cutoff)
            .Select(entry => entry.ID)
            .ToList();
        var items = new ItemProgress(progress, unused.Count);
        items.Report(0);
        foreach (var id in unused)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await providerScheduler.SchedulePurge(id, cancellationToken: cancellationToken).ConfigureAwait(false);
            items.Increment();
        }

        logger.LogInformation("Queued the purge of {Count} unused entries of {Source}.", unused.Count, source);
        return unused.Count;
    }

    /// <summary>
    ///   When an entry was last refreshed, or for one that never was, such as
    ///   one only quick-refreshed for a preview, when it was last stored.
    /// </summary>
    /// <param name="entry">The series, film or collection.</param>
    /// <returns>The time, in local time, or <c>null</c> when there is none.</returns>
    private DateTime? GetLastTouchedAt(IMetadata entry)
        => MetadataRefreshState.LastRefreshedAt(entry)?.ToLocalTime() ?? entry switch
        {
            Metadata_Series series => series.LastUpdatedAt,
            Metadata_Movie movie => movie.LastUpdatedAt,
            Metadata_Collection collection => collection.LastUpdatedAt,
            IWithUpdateDate updated when entry.ID.Source.IsCore => updated.LastUpdatedAt.ToLocalTime(),
            _ => null,
        };

    /// <inheritdoc />
    public async Task<int> PurgeCollections(MetadataSource source, IProgress<decimal>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!providerScheduler.IsPurgeable(source))
            return 0;

        var collections = metadataService.GetAllCollectionsForSource(source).Select(collection => collection.ID).ToList();
        var items = new ItemProgress(progress, collections.Count);
        items.Report(0);
        foreach (var id in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await providerScheduler.SchedulePurge(id, force: true, cancellationToken).ConfigureAwait(false);
            items.Increment();
        }

        logger.LogInformation("Queued the purge of {Count} collections of {Source}.", collections.Count, source);
        return collections.Count;
    }

    #endregion

    #region Orphans

    /// <inheritdoc />
    public async Task<int> PurgeOrphaned(
        MetadataSource? source = null,
        DateTime? orphanedBefore = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var cutoff = orphanedBefore ?? DateTime.Now.AddDays(-settingsProvider.GetSettings().Metadata.PurgeOrphanedAfterDays);
        IReadOnlyList<MetadataSource> sources = source is not null
            ? source.IsCore ? [] : [source]
            : creators.GetAll().Select(creator => creator.Source)
                .Concat(characters.GetAll().Select(character => character.Source))
                .Concat(studios.GetAll().Select(studio => studio.Source))
                .Concat(networks.GetAll().Select(network => network.Source))
                .Concat(tags.GetAll().Select(tag => tag.Source))
                .Where(each => !each.IsCore)
                .Distinct()
                .ToList();

        // The orphans of each source, the leftovers, and the refreshes of
        // linked leftovers.
        var stages = new StagedProgress(progress, 1, 2, 1);
        stages.Report(0);
        var sourceItems = new ItemProgress(stages, sources.Count);
        var total = 0;
        foreach (var each in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var people = peopleStore.RemoveOrphaned(each, cutoff);
            var organisations = studioStore.RemoveOrphaned(each, cutoff);
            var unusedTags = tagStore.RemoveUnused(each, cutoff);
            cleanup.RemoveImageLinks([.. people, .. organisations, .. unusedTags]);
            if (people.Count + organisations.Count + unusedTags.Count > 0)
                logger.LogInformation(
                    "Purged {PeopleCount} people, {StudioCount} studios and networks, and {TagCount} unused tags of {Source} orphaned before {Cutoff}.",
                    people.Count,
                    organisations.Count,
                    unusedTags.Count,
                    each,
                    cutoff
                );
            total += people.Count + organisations.Count + unusedTags.Count;
            sourceItems.Increment();
        }

        // What a plugin source's series left behind goes as the series would,
        // and a linked one is refreshed, which stores it again.
        stages.NextStage();
        var (leftovers, linked) = await PurgeStoreLeftovers(source, stages, cancellationToken).ConfigureAwait(false);
        stages.NextStage();
        var linkedItems = new ItemProgress(stages, linked.Count);
        foreach (var entry in linked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Refreshing {Entry}, which is linked but has no row of its own.", entry);
            await providerScheduler.ScheduleRefreshForEntry(entry, cancellationToken: cancellationToken).ConfigureAwait(false);
            linkedItems.Increment();
        }

        total += leftovers;

        stages.Complete();
        return total;
    }

    #endregion

    #region Leftovers

    /// <summary>
    ///   Removes what the plugin sources still store for series that have no
    ///   row of their own and that nothing links to, as removing the series
    ///   does: their seasons, episodes, texts, images and orderings.
    /// </summary>
    /// <param name="source">Only this source's, or every plugin source's when left out.</param>
    /// <param name="progress">Told how far the removal is, from 0 to 100.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many series were cleared, and the linked ones left for a refresh to store again.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal async Task<(int Removed, IReadOnlyList<MetadataGuid> Linked)> PurgeStoreLeftovers(
        MetadataSource? source = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var removed = 0;
        var linked = new List<MetadataGuid>();
        var series = seasonRows.GetAll().Select(season => (season.Source, season.SeriesID))
            .Concat(episodeRows.GetAll().Select(episode => (episode.Source, episode.SeriesID)))
            .Where(pair => !pair.Source.IsCore && (source is null || pair.Source == source))
            .Select(pair => new MetadataGuid(pair.Source, MetadataEntityType.Series, pair.SeriesID))
            .Distinct()
            .Where(IsLeftoverSeries)
            .ToList();
        var items = new ItemProgress(progress, series.Count);
        foreach (var entry in series)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Increment();
            using var entryLock = await entryLocks.Acquire(entry, cancellationToken).ConfigureAwait(false);
            using var imagesLock = await entryLocks.AcquireImages(entry, cancellationToken).ConfigureAwait(false);
            if (!IsLeftoverSeries(entry))
                continue;

            if (crossReferences.IsLinked(entry))
            {
                linked.Add(entry);
                continue;
            }

            using var updating = entryLocks.MarkUpdating(entry);
            logger.LogInformation("Removing what is left of {Entry}, which has no row of its own.", entry);
            seriesStore.RemoveSeries(entry);
            removed++;
        }

        return (removed, linked);
    }

    /// <summary>
    ///   Whether stored seasons or episodes name a series that has no row.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns><c>true</c> when only leftovers name it.</returns>
    private bool IsLeftoverSeries(MetadataGuid series)
        => seriesRows.GetByProviderID(series.Source, series.ID) is null &&
            (seasonRows.GetBySeriesID(series.Source, series.ID).Count > 0 || episodeRows.GetBySeriesID(series.Source, series.ID).Count > 0);

    #endregion
}
