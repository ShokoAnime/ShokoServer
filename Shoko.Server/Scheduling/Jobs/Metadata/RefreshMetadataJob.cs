using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Has one provider refresh one series, film or collection on its source.
/// </summary>
/// <remarks>
///   One job type per provider, so a suspended or limited provider holds back only
///   its own refreshes, and one job per entry, so an entry linked from several
///   anime is refreshed once. The core takes the entry's lock, skips it when it
///   was refreshed within <see cref="MetadataRefreshState.FreshFor"/> unless
///   forced, records the refresh unless it was quick, then queues the follow-ups.
///   A failed refresh keeps its refresh time and fails the job, so the queue retries it.
/// </remarks>
/// <typeparam name="TProvider">The provider to refresh from.</typeparam>
[DatabaseRequired]
[NetworkRequired]
[ProviderJob]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Metadata)]
[JobPriority(Default = 10, Prioritized = 60)]
public class RefreshMetadataJob<TProvider>(
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataCollectionStore collectionStore,
    IMetadataService metadataService,
    IMetadataRefreshState refreshState,
    MetadataEntryLocks entryLocks,
    MetadataProviderScheduler providerScheduler,
    MetadataImageContributorScheduler contributorScheduler,
    MetadataCollectionRefreshScheduler collectionScheduler,
    ISettingsProvider settingsProvider,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob, IMetadataRefreshJob, IJobMerge where TProvider : class, IMetadataProvider
{
    #region Properties

    private MetadataProviderInfo? _providerInfo;

    /// <summary>
    ///   The series, film or collection to refresh, as its
    ///   <see cref="MetadataGuid"/> string.
    /// </summary>
    public string EntryID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether to refresh the entry however recently it was refreshed.
    ///   Never passed on to the provider. Part of the job's key, so a forced
    ///   request is never merged into an unforced one already running.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    ///   Whether this is a quick refresh, which the provider is told about
    ///   and after which no images are queued. Part of the job's key, so a
    ///   full request is never merged into a quick one already running.
    /// </summary>
    public bool QuickRefresh { get; set; }

    /// <summary>
    ///   Whether to refresh the entry even when nothing links to it, so
    ///   somebody can look at it before linking it. Set only for a refresh
    ///   asked for by name. Left out of the job's key.
    /// </summary>
    [JobKeyIgnore]
    public bool AllowUnlinked { get; set; }

    /// <summary>
    ///   Whether to queue the images of what was refreshed afterwards, which
    ///   is skipped after a quick refresh. Left out of the job's key.
    /// </summary>
    [JobKeyIgnore]
    public bool DownloadImages { get; set; }

    /// <summary>
    ///   Whether the provider should fetch a series' alternate orderings, or
    ///   <c>null</c> to go by the settings. Left out of the job's key.
    /// </summary>
    [JobKeyIgnore]
    public bool? DownloadAlternateOrdering { get; set; }

    /// <summary>
    ///   Why the refresh was queued, passed on to the provider. Left out of
    ///   the job's key.
    /// </summary>
    [JobKeyIgnore]
    public MetadataRefreshReason Reason { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Refresh Metadata";

    /// <inheritdoc />
    public override string Title => "Refreshing Metadata";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
        => new Dictionary<string, object> { ["Provider"] = _providerInfo?.Name ?? typeof(TProvider).Name }.WithEntry(EntryID, _providerInfo?.Source);

    /// <inheritdoc />
    public override void PostInit()
        => _providerInfo = MetadataProviderJobContext.Find<TProvider>(providerManager);

    /// <summary>
    ///   Takes in a request for the same entry, keeping whichever asks for
    ///   more: images, the alternate orderings, the entry fetched unlinked,
    ///   and the strongest reason.
    /// </summary>
    /// <param name="incoming">The request merged in.</param>
    /// <returns><c>true</c> when anything changed.</returns>
    public bool TryMerge(IQueueJob incoming)
    {
        if (incoming is not RefreshMetadataJob<TProvider> other)
            return false;

        var changed = false;
        if (!DownloadImages && other.DownloadImages)
        {
            DownloadImages = true;
            changed = true;
        }

        if (!AllowUnlinked && other.AllowUnlinked)
        {
            AllowUnlinked = true;
            changed = true;
        }

        // Fetching the orderings beats the settings, and the settings beat not fetching them.
        static int Rank(bool? orderings) => orderings switch { true => 2, null => 1, false => 0 };
        if (Rank(other.DownloadAlternateOrdering) > Rank(DownloadAlternateOrdering))
        {
            DownloadAlternateOrdering = other.DownloadAlternateOrdering;
            changed = true;
        }

        if (other.Reason > Reason)
        {
            Reason = other.Reason;
            changed = true;
        }

        return changed;
    }

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        if (MetadataProviderJobContext.Resolve<TProvider>(providerManager, _logger) is not { } context)
            return;

        var (info, provider) = context;
        if (!info.Enabled)
        {
            _logger.LogDebug("Not refreshing from {Provider}, which is disabled.", info.Name);
            return;
        }

        if (!MetadataGuid.TryParse(EntryID, out var entry) || entry.Source != info.Source)
        {
            _logger.LogWarning("Not refreshing {EntryID}, which is not an entry on {Source}.", EntryID, info.Source);
            return;
        }

        var token = cancellationAccessor.Token;
        if (await Refresh(info, provider, entry, token).ConfigureAwait(false) is { } isNew)
            await ScheduleFollowUps(info, entry, isNew, token).ConfigureAwait(false);
    }

    /// <summary>
    ///   Has the provider refresh the entry, under the entry's lock, when it
    ///   is still linked and due.
    /// </summary>
    /// <param name="info">The provider's info.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="token">Cancels the work.</param>
    /// <returns>
    ///   Whether this was the entry's first refresh, or <c>null</c> when
    ///   the provider did not refresh it.
    /// </returns>
    /// <exception cref="OperationCanceledException">The job was cancelled.</exception>
    /// <exception cref="Exception">The provider failed to refresh the entry.</exception>
    private async Task<bool?> Refresh(MetadataProviderInfo info, TProvider provider, MetadataGuid entry, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var kind = entry.EntityType;
        if (!MetadataProviderScheduler.MayRefresh(info, kind))
        {
            _logger.LogDebug("Not refreshing {Entry}: {Provider} is not enabled for {Kind}.", entry, info.Name, kind);
            return null;
        }

        Func<MetadataRefreshOptions, Task>? refresh = provider switch
        {
            IMetadataSeriesProvider series when kind == MetadataEntityType.Series => options => series.RefreshSeries(entry, options, token),
            IMetadataMovieProvider movie when kind == MetadataEntityType.Movie => options => movie.RefreshMovie(entry, options, token),
            IMetadataCollectionProvider collection when kind == MetadataEntityType.Collection => options => collection.RefreshCollection(entry, options, token),
            _ => null,
        };
        if (refresh is null)
        {
            _logger.LogDebug("Not refreshing {Entry}: {Provider} does not refresh a {Kind}.", entry, info.Name, kind);
            return null;
        }

        using var entryLock = await entryLocks.Acquire(entry, token).ConfigureAwait(false);

        // Checked under the lock, as a purge or unlink may have come first. An entry asked for
        // by name is fetched even when unlinked, so it can be looked at before linking.
        if (!AllowUnlinked && !IsStillLinked(entry))
        {
            _logger.LogDebug("Not refreshing {Entry}, which is no longer linked.", entry);
            return null;
        }

        var lastRefreshedAt = refreshState.GetLastRefreshedAt(entry);
        if (!Force && MetadataRefreshState.IsFresh(lastRefreshedAt))
        {
            _logger.LogDebug("Not refreshing {Entry}, which {Provider} last refreshed at {LastRefreshedAt}.", entry, info.Name, lastRefreshedAt);
            return null;
        }

        try
        {
            // A forced refresh vouches for nothing fetched before, so the
            // provider is not told when that was.
            using var updating = entryLocks.MarkUpdating(entry);
            await refresh(this.ToOptions() with { LastRefreshedAt = Force ? null : lastRefreshedAt }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            _logger.LogError(ex, "{Provider} failed to refresh {Entry}.", info.Name, entry);
            throw;
        }

        // A quick refresh leaves out what is costly to fetch, so the next
        // refresh that is not forced must still run in full.
        if (!QuickRefresh)
            refreshState.RecordRefresh(entry, DateTime.UtcNow);

        _logger.LogDebug("{Provider} refreshed {Entry}.", info.Name, entry);
        return lastRefreshedAt is null;
    }

    /// <summary>
    ///   Whether an entry is still something the core refreshes: a series or
    ///   film something links to, a series episode links point into, or a
    ///   collection that is stored or that a linked film names.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns><c>true</c> when it is.</returns>
    private bool IsStillLinked(MetadataGuid entry)
        => entry.EntityType == MetadataEntityType.Collection
            ? metadataService.GetEntry(entry) is ICollection || collectionScheduler.IsWanted(entry)
            : crossReferences.IsLinked(entry);

    /// <summary>
    ///   Queues what follows a refresh: a series' episode link sync; unless it
    ///   was quick, the episode matching of every anime linked to a series and
    ///   the collection a film names; and, when images were asked for and it
    ///   was not quick, the images of the entry and of the stored collections
    ///   holding it, through the owner's image job or else the contributors' jobs.
    /// </summary>
    /// <param name="info">The provider's info.</param>
    /// <param name="entry">The entry the provider refreshed.</param>
    /// <param name="isNew">Whether this was its first refresh, so its images are new.</param>
    /// <param name="token">Cancels the work.</param>
    /// <returns>A task that completes once the jobs are queued.</returns>
    /// <exception cref="OperationCanceledException">The job was cancelled.</exception>
    private async Task ScheduleFollowUps(MetadataProviderInfo info, MetadataGuid entry, bool isNew, CancellationToken token)
    {
        // A quick refresh leaves out too much to match against.
        if (!QuickRefresh)
        {
            foreach (var anidbAnimeID in GetAnimeToMatch(entry))
                await Queue(
                    "the episode matching",
                    $"AniDB anime {anidbAnimeID}",
                    () => providerScheduler.ScheduleEpisodeMatch(info.Source, anidbAnimeID, token)
                ).ConfigureAwait(false);

            // The provider names a film's collection by ID; the core fetches it.
            if (entry.EntityType == MetadataEntityType.Movie)
                collectionScheduler.ScheduleIfDue(metadataService.GetMovie(entry)?.CollectionID);
        }

        if (entry.EntityType == MetadataEntityType.Series)
            await Queue("the episode link sync", entry.ToString(), () => providerScheduler.ScheduleLinkSync(entry, token)).ConfigureAwait(false);

        if (!DownloadImages || QuickRefresh)
            return;

        // The owner's image job queues the contributors once it has run, so
        // they are queued here only for what it does not run for.
        var ownerImages = info.Provider is IMetadataImageProvider &&
            settingsProvider.GetSettings().Metadata.GetImageSettings(info.Source).AnyEnabled;
        var collections = entry.EntityType == MetadataEntityType.Collection
            ? []
            : collectionStore.GetCollectionsWith(entry)
                .Select(collection => collection.ID)
                .Where(collection => collection.Source == info.Source)
                .Distinct()
                .ToList();
        await QueueImages(info, entry, ownerImages, isNew, token).ConfigureAwait(false);
        foreach (var collection in collections)
            await QueueImages(info, collection, ownerImages, isNew: false, token).ConfigureAwait(false);
    }

    /// <summary>
    ///   Queues the images of one entry through the owner's image job, or
    ///   else through the contributors' jobs.
    /// </summary>
    /// <param name="info">The provider's info.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="ownerImages">Whether the provider's own image job may run for the source.</param>
    /// <param name="isNew">Whether this is the entry's first image run.</param>
    /// <param name="token">Cancels the work.</param>
    /// <returns>A task that completes once the jobs are queued.</returns>
    /// <exception cref="OperationCanceledException">The job was cancelled.</exception>
    private Task QueueImages(MetadataProviderInfo info, MetadataGuid entry, bool ownerImages, bool isNew, CancellationToken token)
        => Queue("the images", entry.ToString(), async () =>
        {
            if (!ownerImages || !await providerScheduler.ScheduleImages(info, entry, cancellationToken: token, isNew: isNew).ConfigureAwait(false))
                await contributorScheduler.ScheduleForEntry(entry, isNew: isNew, cancellationToken: token).ConfigureAwait(false);
        });

    /// <summary>
    ///   The anime whose episodes are matched again after a series was
    ///   refreshed: every anime linked to it. The queue merges the matching of
    ///   an anime asked for by several of its entries.
    /// </summary>
    /// <param name="entry">The entry the provider refreshed.</param>
    /// <returns>The AniDB anime IDs, each once.</returns>
    private List<int> GetAnimeToMatch(MetadataGuid entry)
        => entry.EntityType != MetadataEntityType.Series
            ? []
            : [
                .. crossReferences.GetLinksTo(entry)
                    .OfType<IMetadataSeriesCrossReference>()
                    .Select(link => link.AnidbAnimeID)
                    .Where(anidbAnimeID => anidbAnimeID > 0)
                    .Distinct(),
            ];

    /// <summary>
    ///   Queues one follow-up job, logging a failure rather than failing the
    ///   refresh that already happened.
    /// </summary>
    /// <param name="what">What is queued, for the log.</param>
    /// <param name="subject">What it is queued for, for the log.</param>
    /// <param name="queue">Queues it.</param>
    /// <returns>A task that completes once it is queued.</returns>
    /// <exception cref="OperationCanceledException">The job was cancelled.</exception>
    private async Task Queue(string what, string subject, Func<Task> queue)
    {
        try
        {
            await queue().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to queue {What} of {Subject}.", what, subject);
        }
    }

    #endregion
}
