using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Has one provider refresh what is linked to an anime on its source, or
///   one entry that is still linked or that somebody asked for by name.
/// </summary>
/// <remarks>
///   One job type per provider, so a paused or limited provider holds back only
///   its own refreshes. The provider writes the stores itself; the core takes each
///   entry's lock, skips one refreshed within <see cref="MetadataRefreshState.FreshFor"/>
///   unless forced, records the refresh unless it was quick, then queues the episode
///   link sync, the episode matching of the anime and of every anime linked to a
///   refreshed series unless it was quick, and, when asked, the images. A failed entry
///   keeps its refresh time and does not stop the rest, but the job fails afterwards so
///   the queue retries it.
/// </remarks>
/// <typeparam name="TProvider">The provider to refresh from.</typeparam>
[DatabaseRequired]
[MetadataProviderJob]
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
    ISettingsProvider settingsProvider,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob, IMetadataRefreshJob where TProvider : class, IMetadataProvider
{
    #region Properties

    private MetadataProviderInfo? _providerInfo;

    /// <summary>
    ///   The AniDB anime whose linked entries are refreshed, or, with
    ///   <see cref="EntryID"/>, the anime the entry was refreshed for. 0 when
    ///   there is none.
    /// </summary>
    public int AnimeID { get; set; }

    /// <summary>
    ///   One series, film or collection to refresh instead of everything the
    ///   anime links to, as its <see cref="MetadataGuid"/> string.
    /// </summary>
    public string? EntryID { get; set; }

    /// <summary>
    ///   Whether to refresh the entries however recently they were refreshed.
    ///   Never passed on to the provider.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    ///   Whether to refresh the entry asked for with <see cref="EntryID"/>
    ///   even when nothing links to it, so somebody can look at it before
    ///   linking it. Set only for a refresh asked for by name; every other
    ///   refresh skips an entry that is no longer linked when its turn comes.
    /// </summary>
    public bool AllowUnlinked { get; set; }

    /// <summary>
    ///   Whether to queue the images of what was refreshed afterwards, which
    ///   is skipped after a quick refresh.
    /// </summary>
    public bool DownloadImages { get; set; }

    /// <summary>
    ///   Whether the provider should fetch the cast and crew, or
    ///   <c>null</c> to go by the settings.
    /// </summary>
    public bool? DownloadCrewAndCast { get; set; }

    /// <summary>
    ///   Whether the provider should fetch a series' alternate orderings, or
    ///   <c>null</c> to go by the settings.
    /// </summary>
    public bool? DownloadAlternateOrdering { get; set; }

    /// <summary>
    ///   Whether the provider should fetch the networks a series aired on, or
    ///   <c>null</c> to go by the settings.
    /// </summary>
    public bool? DownloadNetworks { get; set; }

    /// <summary>
    ///   Whether the provider should fetch the collections a film belongs
    ///   to, or <c>null</c> to go by the settings.
    /// </summary>
    public bool? DownloadCollections { get; set; }

    /// <summary>
    ///   Whether this is a quick refresh, which the provider is told about
    ///   and after which no images are queued.
    /// </summary>
    public bool QuickRefresh { get; set; }

    /// <summary>
    ///   Why the refresh was queued, passed on to the provider. Left out of
    ///   the job's key, so requests that differ only in why they were made
    ///   merge.
    /// </summary>
    [JobKeyIgnore]
    public MetadataRefreshReason Reason { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Refresh Metadata";

    /// <inheritdoc />
    public override string Title => "Refreshing Metadata";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object> { ["Provider"] = _providerInfo?.Name ?? typeof(TProvider).Name };
            if (AnimeID > 0)
                details["AnimeID"] = AnimeID;
            return details.WithEntry(EntryID);
        }
    }

    /// <inheritdoc />
    public override void PostInit()
        => _providerInfo = MetadataProviderJobContext.Find<TProvider>(providerManager);

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

        var entries = GetEntries(info.Source);
        if (entries is null)
            return;

        var token = cancellationAccessor.Token;
        var failures = new List<Exception>();
        var refreshed = new List<MetadataGuid>();
        var firstRefreshed = new HashSet<MetadataGuid>();
        foreach (var entry in entries)
            if (await Refresh(info, provider, entry, failures, firstRefreshed, token).ConfigureAwait(false))
                refreshed.Add(entry);

        await ScheduleFollowUps(info, refreshed, firstRefreshed, token).ConfigureAwait(false);

        if (failures.Count is 1)
            throw failures[0];
        if (failures.Count > 1)
            throw new AggregateException($"Refreshing from {info.Name} failed for {failures.Count} entries.", failures);
    }

    /// <summary>
    ///   The entries to refresh: the one asked for, or every series and film
    ///   the anime links to on the source, with the series its episode links
    ///   point into.
    /// </summary>
    /// <param name="source">The provider's source.</param>
    /// <returns>The entries, or <c>null</c> when the one asked for is not valid.</returns>
    private List<MetadataGuid>? GetEntries(MetadataSource source)
    {
        if (EntryID is not null)
        {
            if (!MetadataGuid.TryParse(EntryID, out var entry) || entry.Source != source)
            {
                _logger.LogWarning("Not refreshing {EntryID}, which is not an entry on {Source}.", EntryID, source);
                return null;
            }

            return [entry];
        }

        if (AnimeID <= 0)
            return [];

        return crossReferences.GetLinkedEntries(AnimeID, source);
    }

    /// <summary>
    ///   Has the provider refresh one entry, under the entry's lock, when it
    ///   is still linked and due.
    /// </summary>
    /// <param name="info">The provider's info.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="failures">Where a failure is collected.</param>
    /// <param name="firstRefreshed">Where an entry refreshed for the first time is collected.</param>
    /// <param name="token">Cancels the work.</param>
    /// <returns>Whether the provider refreshed the entry.</returns>
    /// <exception cref="OperationCanceledException">The job was cancelled.</exception>
    private async Task<bool> Refresh(
        MetadataProviderInfo info,
        TProvider provider,
        MetadataGuid entry,
        List<Exception> failures,
        HashSet<MetadataGuid> firstRefreshed,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        var kind = entry.EntityType;
        if (!MetadataProviderScheduler.MayRefresh(info, kind))
        {
            _logger.LogDebug("Not refreshing {Entry}: {Provider} is not enabled for {Kind}.", entry, info.Name, kind);
            return false;
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
            return false;
        }

        using var entryLock = await entryLocks.Acquire(entry, token).ConfigureAwait(false);

        // Checked under the lock, as a purge or unlink may have come first. An entry asked for
        // by name is fetched even when unlinked, so it can be looked at before linking.
        if (!(AllowUnlinked && EntryID is not null) && !IsStillLinked(entry))
        {
            _logger.LogDebug("Not refreshing {Entry}, which is no longer linked.", entry);
            return false;
        }

        var lastRefreshedAt = refreshState.GetLastRefreshedAt(entry);
        if (!Force && MetadataRefreshState.IsFresh(lastRefreshedAt))
        {
            _logger.LogDebug("Not refreshing {Entry}, which {Provider} last refreshed at {LastRefreshedAt}.", entry, info.Name, lastRefreshedAt);
            return false;
        }

        try
        {
            // A forced refresh vouches for nothing fetched before, so the
            // provider is not told when that was.
            using var updating = entryLocks.MarkUpdating(entry);
            await refresh(this.ToOptions() with
            {
                LastRefreshedAt = Force ? null : lastRefreshedAt,
                AnidbAnimeID = AnimeID > 0 ? AnimeID : null,
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider} failed to refresh {Entry}.", info.Name, entry);
            failures.Add(ex);
            return false;
        }

        // A quick refresh leaves out what is costly to fetch, so the next
        // refresh that is not forced must still run in full.
        if (!QuickRefresh)
            refreshState.RecordRefresh(entry, DateTime.UtcNow);

        if (lastRefreshedAt is null)
            firstRefreshed.Add(entry);

        _logger.LogDebug("{Provider} refreshed {Entry}.", info.Name, entry);
        return true;
    }

    /// <summary>
    ///   Whether an entry is still something the core refreshes: a series or
    ///   film something links to, a series episode links point into, or a
    ///   stored collection.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns><c>true</c> when it is.</returns>
    private bool IsStillLinked(MetadataGuid entry)
        => entry.EntityType == MetadataEntityType.Collection
            ? metadataService.GetCollection(entry) is not null
            : crossReferences.IsLinked(entry);

    /// <summary>
    ///   Queues what follows a refresh: the sync of each refreshed series'
    ///   episode links, the episode matching unless this is a quick refresh,
    ///   and, when images were asked for and this is not a quick refresh, the
    ///   images of the refreshed entries and the stored collections holding
    ///   them: the owner's image job, or the image contributors' jobs when the
    ///   owner's does not run.
    /// </summary>
    /// <param name="info">The provider's info.</param>
    /// <param name="refreshed">The entries the provider refreshed.</param>
    /// <param name="firstRefreshed">The entries refreshed for the first time, whose images are new.</param>
    /// <param name="token">Cancels the work.</param>
    /// <returns>A task that completes once the jobs are queued.</returns>
    private async Task ScheduleFollowUps(MetadataProviderInfo info, List<MetadataGuid> refreshed, HashSet<MetadataGuid> firstRefreshed, CancellationToken token)
    {
        // A quick refresh leaves out too much to match against.
        if (!QuickRefresh)
        {
            foreach (var anidbAnimeID in GetAnimeToMatch(refreshed))
                await Queue(
                    "the episode matching",
                    $"AniDB anime {anidbAnimeID}",
                    () => providerScheduler.ScheduleEpisodeMatch(info.Source, anidbAnimeID, token)
                ).ConfigureAwait(false);
        }

        if (refreshed.Count is 0)
            return;

        foreach (var entry in refreshed.Where(entry => entry.EntityType == MetadataEntityType.Series))
            await Queue("the episode link sync", entry.ToString(), () => providerScheduler.ScheduleLinkSync(entry, token)).ConfigureAwait(false);

        if (!DownloadImages || QuickRefresh)
            return;

        // The owner's image job queues the contributors once it has run, so
        // they are queued here only for what it does not run for.
        var ownerImages = info.Provider is IMetadataImageProvider &&
            settingsProvider.GetSettings().Image.GetMetadataSourceSettings(info.Source).AnyEnabled;
        var withImages = refreshed
            .Concat(refreshed
                .Where(entry => entry.EntityType != MetadataEntityType.Collection)
                .SelectMany(entry => collectionStore.GetCollectionsWith(entry))
                .Select(collection => collection.ID)
                .Where(collection => collection.Source == info.Source))
            .Distinct()
            .ToList();
        foreach (var entry in withImages)
        {
            var isNew = firstRefreshed.Contains(entry);
            await Queue("the images", entry.ToString(), async () =>
            {
                if (!ownerImages || !await providerScheduler.ScheduleImages(info, entry, cancellationToken: token, isNew: isNew).ConfigureAwait(false))
                    await contributorScheduler.ScheduleForEntry(entry, isNew: isNew, cancellationToken: token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///   The anime whose episodes are matched again after the job: the one
    ///   it ran for, whether or not anything was due, and every anime linked
    ///   to a series it refreshed.
    /// </summary>
    /// <param name="refreshed">The entries the provider refreshed.</param>
    /// <returns>The AniDB anime IDs, each once.</returns>
    private List<int> GetAnimeToMatch(List<MetadataGuid> refreshed)
    {
        var anime = new List<int>();
        if (AnimeID > 0)
            anime.Add(AnimeID);

        anime.AddRange(refreshed
            .Where(entry => entry.EntityType == MetadataEntityType.Series)
            .SelectMany(crossReferences.GetLinksTo)
            .OfType<IMetadataSeriesCrossReference>()
            .Select(link => link.AnidbAnimeID)
            .Where(anidbAnimeID => anidbAnimeID > 0));
        return [.. anime.Distinct()];
    }

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
