using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   Queues the core's provider jobs: refreshes, searches, images and
///   purges, one job type per provider.
/// </summary>
/// <remarks>
///   Every provider runs through the same jobs, so every caller asks here
///   whatever the source.
/// </remarks>
/// <param name="providerManager">The registered providers.</param>
/// <param name="crossReferences">The links, to tell which sources an anime is linked on.</param>
/// <param name="scheduler">The queue.</param>
/// <param name="jobFactory">Runs a job at once, for a caller that waits for it.</param>
/// <param name="logger">Where skipped requests are reported.</param>
/// <param name="entityScheduler">Routes the refresh of a creator, character, studio or network.</param>
public class MetadataProviderScheduler(
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IQueueScheduler scheduler,
    IJobFactory jobFactory,
    ILogger<MetadataProviderScheduler> logger,
    MetadataEntityRefreshScheduler? entityScheduler = null
)
{
    private static readonly MethodInfo _executeNow = typeof(MetadataProviderScheduler)
        .GetMethod(nameof(ExecuteNow), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    ///   A full refresh that goes by the settings and downloads the images,
    ///   for a reason.
    /// </summary>
    /// <param name="reason">Why the refresh is asked for.</param>
    /// <returns>The options.</returns>
    public static MetadataRefreshOptions FullRefresh(MetadataRefreshReason reason = MetadataRefreshReason.Scheduled)
        => new() { DownloadImages = true, Reason = reason };

    #region Refresh

    /// <summary>
    ///   Queue a refresh of everything an anime links to, from every enabled
    ///   provider whose source it has links on, or from one source.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <param name="force">
    ///   Whether to refresh every entry however recently it was refreshed.
    ///   A forced refresh is never told to the providers as
    ///   <see cref="MetadataRefreshReason.Scheduled"/>.
    /// </param>
    /// <param name="options">What to fetch, or <c>null</c> for <see cref="FullRefresh"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many providers were asked.</returns>
    public async Task<int> ScheduleRefreshForAnime(
        int anidbAnimeID,
        MetadataSource? source = null,
        bool force = false,
        MetadataRefreshOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (anidbAnimeID <= 0)
            return 0;

        var asked = 0;
        foreach (var info in providerManager.MetadataProviders.Where(info => info.Enabled && (source is null || info.Source == source)))
        {
            try
            {
                if (await ScheduleRefresh(info, anidbAnimeID, null, force, options, cancellationToken).ConfigureAwait(false))
                    asked++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One provider failing to queue is no reason for the rest to
                // go unasked.
                logger.LogError(ex, "Failed to queue a refresh from {Provider} for anime {AnimeID}.", info.Name, anidbAnimeID);
            }
        }

        return asked;
    }

    /// <summary>
    ///   Queue a refresh from one provider of everything an anime links to on
    ///   its source, or of one entry.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="anidbAnimeID">
    ///   The AniDB anime ID: the anime whose linked entries are refreshed,
    ///   or the one <paramref name="entryID"/> is refreshed for. 0 when there
    ///   is none.
    /// </param>
    /// <param name="entryID">One linked entry to refresh instead of everything, or <c>null</c>.</param>
    /// <param name="force">Whether to refresh the entries however recently they were refreshed.</param>
    /// <param name="options">
    ///   What to fetch and why, or <c>null</c> for
    ///   <see cref="FullRefresh"/>. A forced refresh is never told it was
    ///   <see cref="MetadataRefreshReason.Scheduled"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <param name="immediate">Whether to run the refresh now and wait for it rather than queue it.</param>
    /// <param name="allowUnlinked">
    ///   Whether to refresh <paramref name="entryID"/> even when nothing links
    ///   to it, for an entry somebody asked for by name.
    /// </param>
    /// <param name="prioritize">Whether to queue it ahead of the rest even though it is not forced.</param>
    /// <returns>
    ///   <c>true</c> when something was queued or ran, or
    ///   <c>false</c> when the anime has no links on the source,
    ///   the provider refreshes nothing, or it was asked to run at once while
    ///   it cannot.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
    public async Task<bool> ScheduleRefresh(
        MetadataProviderInfo info,
        int anidbAnimeID,
        MetadataGuid? entryID = null,
        bool force = false,
        MetadataRefreshOptions? options = null,
        CancellationToken cancellationToken = default,
        bool immediate = false,
        bool allowUnlinked = false,
        bool prioritize = false
    )
    {
        ArgumentNullException.ThrowIfNull(info);
        if (entryID is null && !IsLinked(anidbAnimeID, info.Source))
            return false;

        if (MetadataProviderJobs.GetRefreshJobType(info.Provider.GetType()) is not { } jobType)
            return false;

        options ??= FullRefresh();
        if (force && options.Reason is MetadataRefreshReason.Scheduled)
            options = options with { Reason = MetadataRefreshReason.Requested };

        return await Dispatch(info, jobType, job =>
        {
            var refresh = (IMetadataRefreshJob)job;
            refresh.AnimeID = Math.Max(anidbAnimeID, 0);
            refresh.EntryID = entryID?.ToString();
            refresh.Force = force;
            refresh.AllowUnlinked = allowUnlinked && entryID is not null;
            refresh.Apply(options);
        }, force || prioritize, immediate).ConfigureAwait(false);
    }

    /// <summary>
    ///   Queue a refresh of one series, film or collection from the provider
    ///   refreshing its kind on its source, or of one creator, character,
    ///   studio or network from the provider taking its kind.
    /// </summary>
    /// <remarks>
    ///   The refresh job still refreshes a series or film only while
    ///   something links to it, and a collection only while it is stored,
    ///   unless the caller's options say it was
    ///   <see cref="MetadataRefreshReason.Requested"/>. A forced refresh made
    ///   <see cref="MetadataRefreshReason.Requested"/> by the force alone
    ///   still needs the link. The options mean nothing to a creator,
    ///   character, studio or network, which is refreshed whenever asked.
    /// </remarks>
    /// <param name="entryID">The series, film, collection, creator, character, studio or network.</param>
    /// <param name="force">Whether to refresh it however recently it was refreshed.</param>
    /// <param name="options">What to fetch and why, or <c>null</c> for <see cref="FullRefresh"/>.</param>
    /// <param name="immediate">Whether to run the refresh now and wait for it rather than queue it.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest even though it is not forced.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <c>true</c> when a refresh was queued or ran, or
    ///   <c>false</c> when no enabled provider refreshes the entry
    ///   or it was asked to run at once while it cannot.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <c>null</c>.</exception>
    public Task<bool> ScheduleRefreshForEntry(
        MetadataGuid entryID,
        bool force = false,
        MetadataRefreshOptions? options = null,
        bool immediate = false,
        bool prioritize = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entryID);
        if (MetadataEntityRefreshScheduler.EntityKinds.Contains(entryID.EntityType))
            return entityScheduler?.ScheduleRefresh(entryID, force, immediate, prioritize) ?? Task.FromResult(false);

        var info = providerManager.MetadataProviders.FirstOrDefault(info =>
            info.Source == entryID.Source &&
            Refreshes(info.Provider, entryID.EntityType) &&
            MayRefresh(info, entryID.EntityType)
        );
        if (info is null)
        {
            logger.LogDebug("Not refreshing {Entry}: no enabled provider refreshes it.", entryID);
            return Task.FromResult(false);
        }

        // Only a request naming the entry fetches it unlinked; read before a
        // forced refresh has its reason turned into a request.
        var allowUnlinked = options?.Reason is MetadataRefreshReason.Requested;
        return ScheduleRefresh(info, 0, entryID, force, options, cancellationToken, immediate, allowUnlinked, prioritize);
    }

    /// <summary>
    ///   Queue the sync of the episode links naming a stored series'
    ///   episodes, merging with one the series store already queued.
    /// </summary>
    /// <param name="seriesID">The series, on a plugin source.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the sync is queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seriesID"/> is <c>null</c>.</exception>
    public async Task ScheduleLinkSync(MetadataGuid seriesID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (seriesID.Source.IsCore || seriesID.EntityType != MetadataEntityType.Series)
            return;

        await scheduler.Enqueue<SyncEpisodeLinksJob>(job =>
        {
            job.Source = seriesID.Source.Value;
            job.SeriesID = seriesID.ID;
        }, ct: cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Images

    /// <summary>
    ///   Queue the image job of one provider for a stored entry.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="entryID">The series, film, collection, creator, character, studio or network, on the provider's source.</param>
    /// <param name="force">Whether to download the desired images again even when they are there.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <param name="immediate">Whether to run the job now and wait for it rather than queue it.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest even though it is not forced.</param>
    /// <param name="isNew">Whether this is the entry's first image run, after it was just linked or created.</param>
    /// <returns>
    ///   <c>true</c> when the job was queued or ran, or
    ///   <c>false</c> when the provider supplies no images, the
    ///   entry is on another source or of a kind the provider is not enabled
    ///   for, or it was asked to run at once while it cannot.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public async Task<bool> ScheduleImages(
        MetadataProviderInfo info,
        MetadataGuid entryID,
        bool force = false,
        CancellationToken cancellationToken = default,
        bool immediate = false,
        bool prioritize = false,
        bool isNew = false
    )
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(entryID);
        if (entryID.Source != info.Source || MetadataProviderJobs.GetImagesJobType(info.Provider.GetType()) is not { } jobType)
            return false;

        if (!MayRefresh(info, entryID.EntityType))
        {
            logger.LogDebug("Not downloading images for {Entry}: {Provider} is not enabled for it.", entryID, info.Name);
            return false;
        }

        return await Dispatch(
            info,
            jobType,
            job =>
            {
                var images = (IMetadataImagesJob)job;
                images.EntryID = entryID.ToString();
                images.Force = force;
                images.IsNew = isNew;
            },
            force || prioritize,
            immediate,
            JobPriorities.ForMetadataEntry(entryID.EntityType, isNew, force || prioritize)
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///   Queue the image job of the enabled provider supplying the images of a
    ///   stored entry's source.
    /// </summary>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="force">Whether to download the desired images again even when they are there.</param>
    /// <param name="immediate">Whether to run the job now and wait for it rather than queue it.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest even though it is not forced.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <c>true</c> when the job was queued or ran, or
    ///   <c>false</c> when no enabled provider supplies images for
    ///   the source, or it was asked to run at once while it cannot.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <c>null</c>.</exception>
    public async Task<bool> ScheduleImagesForEntry(
        MetadataGuid entryID,
        bool force = false,
        bool immediate = false,
        bool prioritize = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entryID);
        foreach (var info in GetImageProviders(entryID.Source))
            if (await ScheduleImages(info, entryID, force, cancellationToken, immediate, prioritize).ConfigureAwait(false))
                return true;

        return false;
    }

    /// <summary>
    ///   Queue the image job of one provider for every series and film an
    ///   anime links to on its source.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="force">Whether to download the desired images again even when they are there.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many image jobs were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
    public async Task<int> ScheduleImagesForAnime(MetadataProviderInfo info, int anidbAnimeID, bool force = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (anidbAnimeID <= 0)
            return 0;

        var entries = crossReferences.GetLinkedEntries(anidbAnimeID, info.Source);
        var queued = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ScheduleImages(info, entry, force, cancellationToken).ConfigureAwait(false))
                queued++;
        }

        return queued;
    }

    /// <summary>
    ///   Whether an enabled provider supplies the images of an entry through
    ///   the core's image job, being on its source and enabled for its kind.
    /// </summary>
    /// <param name="entryID">The series, film or collection.</param>
    /// <returns><c>true</c> when one does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <c>null</c>.</exception>
    public bool HasImageJobFor(MetadataGuid entryID)
    {
        ArgumentNullException.ThrowIfNull(entryID);
        return GetImageProviders(entryID.Source).Any(info =>
            MetadataProviderJobs.GetImagesJobType(info.Provider.GetType()) is not null && MayRefresh(info, entryID.EntityType));
    }

    /// <summary>
    ///   The enabled providers supplying images through the core's image job,
    ///   on one source or on all of them.
    /// </summary>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The providers.</returns>
    public IEnumerable<MetadataProviderInfo> GetImageProviders(MetadataSource? source = null)
        => providerManager.MetadataProviders.Where(info =>
            info.Enabled && info.Provider is IMetadataImageProvider && (source is null || info.Source == source));

    #endregion

    #region Search

    /// <summary>
    ///   Queue a search by a source's auto-linker for an anime.
    /// </summary>
    /// <param name="source">The source to search.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="force">
    ///   Whether to ignore the auto-link setting and the anime's veto, and to
    ///   refresh what is linked however fresh it is.
    /// </param>
    /// <param name="replace">
    ///   Whether a person asked for this one anime, which also searches an
    ///   anime already linked and replaces every link it has on the source
    ///   with what is taken, verified and episode links included. Implies
    ///   <paramref name="force"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <c>true</c> when the search was queued, or
    ///   <c>false</c> when the source has no auto-linker or it is
    ///   not configured.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public async Task<bool> ScheduleSearch(MetadataSource source, int anidbAnimeID, bool force = false, bool replace = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (anidbAnimeID <= 0 || GetAutoLinker(source) is not { } info)
            return false;

        if (!IsConfigured(info, $"anime {anidbAnimeID}"))
            return false;

        if (MetadataProviderJobs.GetSearchJobType(info.Provider.GetType()) is not { } jobType)
            return false;

        return await Dispatch(info, jobType, job =>
        {
            var search = (IMetadataSearchJob)job;
            search.AnimeID = anidbAnimeID;
            search.Force = force || replace;
            search.Replace = replace;
        }, force || replace, immediate: false).ConfigureAwait(false);
    }

    /// <summary>
    ///   The provider set as a source's auto-linker.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The provider, or <c>null</c> when the source has none.</returns>
    public MetadataProviderInfo? GetAutoLinker(MetadataSource source)
        => providerManager.MetadataProviders.FirstOrDefault(info => info.Source == source && info.IsAutoLinker);

    /// <summary>
    ///   Whether an auto-linker has what it needs to search, logging at debug
    ///   level when it does not.
    /// </summary>
    /// <param name="info">The auto-linker.</param>
    /// <param name="subject">What the search was for, for the log.</param>
    /// <returns><c>true</c> when it is configured.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
    public bool IsConfigured(MetadataProviderInfo info, string subject)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Provider.IsConfigured)
            return true;

        logger.LogDebug("Not searching {Source} for {Subject}: {Provider} is not configured.", info.Source, subject, info.Name);
        return false;
    }

    #endregion

    #region Purge

    /// <summary>
    ///   Queue a purge of what the core stores for an entry nothing links to
    ///   any more.
    /// </summary>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="force">
    ///   Whether to purge it even though something still links to it,
    ///   removing those links first.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <c>true</c> once the purge is queued, or
    ///   <c>false</c> when nothing purges the entry's source.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <c>null</c>.</exception>
    public async Task<bool> SchedulePurge(MetadataGuid entryID, bool force = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryID);
        if (!IsPurgeable(entryID.Source))
        {
            logger.LogDebug("Not purging {Entry}: nothing purges that source.", entryID);
            return false;
        }

        await scheduler.Enqueue<PurgeMetadataJob>(job =>
        {
            job.EntryID = entryID.ToString();
            job.Force = force;
        }, prioritize: force, ct: cancellationToken).ConfigureAwait(false);
        return true;
    }

    #endregion

    #region Dispatch

    /// <summary>
    ///   Queues a provider's job, or runs it now and waits for it.
    /// </summary>
    /// <param name="info">The provider the job is for.</param>
    /// <param name="jobType">The job type.</param>
    /// <param name="configure">Sets the job up.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest.</param>
    /// <param name="immediate">Whether to run it now and wait for it.</param>
    /// <param name="priority">The priority to queue it at in place of the type's own, or <c>null</c> for the type's own.</param>
    /// <returns>
    ///   <c>true</c> once it is queued or has run, or
    ///   <c>false</c> when it was to run now but the provider is
    ///   paused or the queue holds its jobs back.
    /// </returns>
    private Task<bool> Dispatch(MetadataProviderInfo info, Type jobType, Action<IQueueJob> configure, bool prioritize, bool immediate, int? priority = null)
        => Dispatch(scheduler, jobFactory, logger, info, jobType, configure, prioritize, immediate, priority);

    /// <summary>
    ///   Queues a provider's job through a queue, or runs it now through a
    ///   job factory and waits for it.
    /// </summary>
    /// <param name="scheduler">The queue.</param>
    /// <param name="jobFactory">Runs the job now.</param>
    /// <param name="logger">Where a refused run is reported.</param>
    /// <param name="info">The provider the job is for.</param>
    /// <param name="jobType">The job type.</param>
    /// <param name="configure">Sets the job up.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest.</param>
    /// <param name="immediate">Whether to run it now and wait for it.</param>
    /// <param name="priority">The priority to queue it at in place of the type's own, or <c>null</c> for the type's own.</param>
    /// <returns>
    ///   <c>true</c> once it is queued or has run, or
    ///   <c>false</c> when it was to run now but the provider is
    ///   paused or the queue holds its jobs back.
    /// </returns>
    internal static async Task<bool> Dispatch(
        IQueueScheduler scheduler,
        IJobFactory jobFactory,
        ILogger logger,
        MetadataProviderInfo info,
        Type jobType,
        Action<IQueueJob> configure,
        bool prioritize,
        bool immediate,
        int? priority = null
    )
    {
        if (!immediate)
        {
            if (priority is { } value)
                await scheduler.EnqueueWithPriority(jobType, configure, value).ConfigureAwait(false);
            else
                await scheduler.Enqueue(jobType, configure, prioritize).ConfigureAwait(false);
            return true;
        }

        if (info.Provider is IPausableMetadataProvider { PauseStatus.IsPaused: true })
        {
            logger.LogInformation("Not running a job for {Provider} at once, as it is paused.", info.Name);
            return false;
        }

        try
        {
            await ((Task)_executeNow.MakeGenericMethod(jobType).Invoke(null, [jobFactory, configure])!).ConfigureAwait(false);
            return true;
        }
        catch (JobBlockedException)
        {
            logger.LogInformation("Not running a job for {Provider} at once, as the queue holds its jobs back.", info.Name);
            return false;
        }
    }

    /// <summary>
    ///   Runs a job of a type known only at runtime through the job factory.
    /// </summary>
    /// <typeparam name="TJob">The job type.</typeparam>
    /// <param name="factory">The job factory.</param>
    /// <param name="configure">Sets the job up.</param>
    /// <returns>A task that completes once the job has run.</returns>
    /// <exception cref="JobBlockedException">The queue holds jobs of the type back.</exception>
    private static Task ExecuteNow<TJob>(IJobFactory factory, Action<IQueueJob> configure) where TJob : class, IQueueJob
        => factory.Execute<TJob>(job => configure(job));

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether the core's purge job purges a source's entries: a plugin
    ///   source, or a core source a provider claims.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><c>true</c> when it does.</returns>
    public bool IsPurgeable(MetadataSource source)
        => IsPurgeable(source, providerManager.MetadataProviders);

    /// <summary>
    ///   Whether the core may purge a source's entries: a plugin source, or a
    ///   core source one of the providers serves.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="providers">The registered providers.</param>
    /// <returns><c>true</c> when it may.</returns>
    public static bool IsPurgeable(MetadataSource source, IEnumerable<MetadataProviderInfo> providers)
        => !source.IsCore || providers.Any(info => info.Source == source);

    /// <summary>
    ///   Whether a provider is turned on for refreshing an entry of a kind.
    /// </summary>
    /// <remarks>
    ///   A series is refreshed whole, with its seasons and episodes, so any of
    ///   <c>series</c>, <c>season</c> and <c>episode</c> will do for it. A
    ///   film or a collection needs its own kind.
    /// </remarks>
    /// <param name="info">The provider.</param>
    /// <param name="entityType">The kind of entry: series, movie or collection.</param>
    /// <returns><c>true</c> when the provider may refresh it.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public static bool MayRefresh(MetadataProviderInfo info, MetadataEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(entityType);

        var enabled = info.EnabledEntityTypes;
        return entityType == MetadataEntityType.Series
            ? enabled.Contains(MetadataEntityType.Series) || enabled.Contains(MetadataEntityType.Season) || enabled.Contains(MetadataEntityType.Episode)
            : enabled.Contains(entityType);
    }

    /// <summary>
    ///   Whether a provider has the refresh call for an entry of a kind.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <returns><c>true</c> for a series, movie or collection provider asked about its own kind.</returns>
    public static bool Refreshes(IMetadataProvider provider, MetadataEntityType entityType)
        => entityType == MetadataEntityType.Series ? provider is IMetadataSeriesProvider
            : entityType == MetadataEntityType.Movie ? provider is IMetadataMovieProvider
            : entityType == MetadataEntityType.Collection && provider is IMetadataCollectionProvider;

    /// <summary>
    ///   Whether an anime is linked to any series or film on a source, counting
    ///   the series its episode links point into.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">The source.</param>
    /// <returns><c>true</c> when it has one.</returns>
    private bool IsLinked(int anidbAnimeID, MetadataSource source)
        => anidbAnimeID > 0 && crossReferences.GetLinkedEntries(anidbAnimeID, source).Count > 0;

    #endregion
}
