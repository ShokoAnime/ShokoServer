using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Services;

/// <summary>
///   Asks the metadata providers for refreshes, images and auto-searches,
///   through the core's jobs for each provider.
/// </summary>
public class MetadataRefreshService : IMetadataRefreshService
{
    private readonly IMetadataProviderManager _providerManager;

    private readonly IMetadataCrossReferenceStore _crossReferences;

    private readonly IMetadataService _metadataService;

    private readonly MetadataProviderScheduler _providerScheduler;

    private readonly MetadataEntryLocks _entryLocks;

    private readonly IMetadataRefreshState _refreshState;

    private readonly MetadataImageContributorScheduler _contributorScheduler;

    /// <summary>
    ///   Asks the providers through the core's jobs.
    /// </summary>
    /// <param name="providerManager">The registered providers.</param>
    /// <param name="crossReferences">The links, to find what a source links to.</param>
    /// <param name="metadataService">
    ///   The Shoko series, for a search of the whole library and to tell the
    ///   anime in it, and every source's stored collections, which nothing
    ///   links to.
    /// </param>
    /// <param name="providerScheduler">Queues the providers' jobs.</param>
    /// <param name="entryLocks">Tells when an entry is being refreshed or purged.</param>
    /// <param name="refreshState">When each entry was last refreshed.</param>
    /// <param name="contributorScheduler">Queues the image contributors' jobs.</param>
    public MetadataRefreshService(
        IMetadataProviderManager providerManager,
        IMetadataCrossReferenceStore crossReferences,
        IMetadataService metadataService,
        MetadataProviderScheduler providerScheduler,
        MetadataEntryLocks entryLocks,
        IMetadataRefreshState refreshState,
        MetadataImageContributorScheduler contributorScheduler
    )
    {
        _providerManager = providerManager;
        _crossReferences = crossReferences;
        _metadataService = metadataService;
        _providerScheduler = providerScheduler;
        _entryLocks = entryLocks;
        _refreshState = refreshState;
        _contributorScheduler = contributorScheduler;
    }

    #region Refresh

    /// <inheritdoc />
    public Task<bool> RefreshEntry(
        MetadataGuid entryID,
        bool force = false,
        MetadataRefreshOptions? options = null,
        bool immediate = false,
        bool prioritize = false,
        CancellationToken cancellationToken = default
    )
        => _providerScheduler.ScheduleRefreshForEntry(entryID, force, options, immediate, prioritize, cancellationToken);

    /// <inheritdoc />
    public Task<int> RefreshForAnime(
        int anidbAnimeID,
        MetadataSource? source = null,
        bool force = false,
        MetadataRefreshOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => _providerScheduler.ScheduleRefreshForAnime(anidbAnimeID, source, force, options, cancellationToken);

    /// <inheritdoc />
    public async Task<int> RefreshAllLinked(
        MetadataSource source,
        bool force = false,
        MetadataRefreshOptions? options = null,
        MetadataEntityType? entityType = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        options ??= MetadataProviderScheduler.FullRefresh(MetadataRefreshReason.Requested);
        var queued = 0;
        var providers = _providerManager.MetadataProviders.Where(info => info.Enabled && info.Source == source).ToList();
        var stages = new StagedProgress(progress, Math.Max(providers.Count, 1));
        stages.Report(0);
        foreach (var info in providers)
        {
            // One refresh for each entry, however many anime link to it, so a
            // forced refresh does not fetch a shared entry once per anime.
            var entries = GetLinkedEntriesInLibrary(info.Source)
                .Where(entry => entityType is null || entry.EntityType == entityType)
                .Distinct()
                .ToList();

            // Nothing links a collection, so each stored one is asked for by name.
            var collections = (entityType is not null && entityType != MetadataEntityType.Collection) || info.Provider is not IMetadataCollectionProvider ||
                !MetadataProviderScheduler.MayRefresh(info, MetadataEntityType.Collection)
                    ? []
                    : _metadataService.GetAllCollectionsForSource(info.Source).ToList();
            var items = new ItemProgress(stages, entries.Count + collections.Count);
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (MetadataProviderScheduler.Refreshes(info.Provider, entry.EntityType) && MetadataProviderScheduler.MayRefresh(info, entry.EntityType) &&
                    await _providerScheduler.ScheduleRefresh(info, entry, force, options, cancellationToken).ConfigureAwait(false))
                    queued++;

                items.Increment();
            }

            foreach (var collection in collections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _providerScheduler.ScheduleRefresh(info, collection.ID, force, options, cancellationToken).ConfigureAwait(false))
                    queued++;

                items.Increment();
            }

            stages.NextStage();
        }

        stages.Complete();
        return queued;
    }

    /// <inheritdoc />
    public bool IsRefreshing(MetadataGuid entryID)
        => _entryLocks.IsUpdating(entryID);

    /// <inheritdoc />
    public Task<bool> WaitForRefresh(MetadataGuid entryID, CancellationToken cancellationToken = default)
        => _entryLocks.WaitForUpdate(entryID, cancellationToken);

    /// <inheritdoc />
    public DateTime? GetLastRefreshedAt(MetadataGuid entryID)
    {
        ArgumentNullException.ThrowIfNull(entryID);

        return _refreshState.GetLastRefreshedAt(entryID);
    }

    #endregion

    #region Images

    /// <inheritdoc />
    public async Task<bool> DownloadImages(
        MetadataGuid entryID,
        bool force = false,
        bool immediate = false,
        bool prioritize = false,
        CancellationToken cancellationToken = default
    )
    {
        // The owner's image job queues the contributors once it has run, so
        // they are only queued here for an entry no provider's job covers.
        if (_providerScheduler.HasImageJobFor(entryID))
            return await _providerScheduler.ScheduleImagesForEntry(entryID, force, immediate, prioritize, cancellationToken).ConfigureAwait(false);

        return await _contributorScheduler.ScheduleForEntry(entryID, force, prioritize, cancellationToken: cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public async Task<int> DownloadImagesForAnime(int anidbAnimeID, MetadataSource? source = null, bool force = false, CancellationToken cancellationToken = default)
    {
        var queued = 0;
        foreach (var info in _providerScheduler.GetImageProviders(source).ToList())
            queued += await _providerScheduler.ScheduleImagesForAnime(info, anidbAnimeID, force, cancellationToken).ConfigureAwait(false);

        return queued;
    }

    /// <inheritdoc />
    public async Task<int> DownloadAllImages(
        MetadataSource source,
        bool force = false,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        var queued = 0;
        var providers = _providerScheduler.GetImageProviders(source).ToList();
        var stages = new StagedProgress(progress, Math.Max(providers.Count, 1));
        stages.Report(0);
        foreach (var info in providers)
        {
            var entries = GetLinkedEntriesInLibrary(info.Source)
                .Distinct()
                .ToList();
            var items = new ItemProgress(stages, entries.Count);
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _providerScheduler.ScheduleImages(info, entry, force, cancellationToken).ConfigureAwait(false))
                    queued++;

                items.Increment();
            }

            stages.NextStage();
        }

        stages.Complete();
        return queued;
    }

    /// <summary>
    ///   Every series and film linked on a source to an anime in the library,
    ///   once for each anime linking to it. A link imported for an anime the
    ///   user does not have is left for when they add it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The entries.</returns>
    private IEnumerable<MetadataGuid> GetLinkedEntriesInLibrary(MetadataSource source)
    {
        var inLibrary = new Dictionary<int, bool>();
        return _crossReferences.GetAllLinkedEntries(source)
            .Where(pair =>
            {
                if (!inLibrary.TryGetValue(pair.AnidbAnimeID, out var found))
                    inLibrary[pair.AnidbAnimeID] = found = _metadataService.GetShokoSeriesByAnidbID(pair.AnidbAnimeID) is not null;
                return found;
            })
            .Select(pair => pair.Entry);
    }

    #endregion

    #region Auto-search

    /// <inheritdoc />
    public Task<bool> AutoSearch(MetadataSource source, int anidbAnimeID, bool force = false, CancellationToken cancellationToken = default)
        => _providerScheduler.ScheduleSearch(source, anidbAnimeID, force, replace: force, cancellationToken);

    /// <inheritdoc />
    public async Task<int> AutoSearchAll(
        MetadataSource source,
        bool force = false,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_providerScheduler.GetAutoLinker(source) is not { } info || !(force || info.AutoLink))
            return 0;

        if (!_providerScheduler.IsConfigured(info, "every anime"))
            return 0;

        var queued = 0;
        var allSeries = _metadataService.GetAllShokoSeries().ToList();
        var items = new ItemProgress(progress, allSeries.Count);
        items.Report(0);
        foreach (var series in allSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The search job checks these too; checking here keeps useless searches out of the queue.
            // A bulk search never replaces links, so a linked anime is left alone even when forced.
            if (_crossReferences.GetLinkedEntries(series.AnidbAnimeID, source).Count is 0 && (force || (
                !series.IsAutoLinkingDisabled(source) &&
                (info.AutoLinkRestricted || series.AnidbAnime is not { Restricted: true }))) &&
                await _providerScheduler.ScheduleSearch(source, series.AnidbAnimeID, force, replace: false, cancellationToken).ConfigureAwait(false))
                queued++;

            items.Increment();
        }

        return queued;
    }

    #endregion
}
