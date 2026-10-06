using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Has one provider refresh one creator, character, studio or network of
///   its source.
/// </summary>
/// <remarks>
///   One job type per provider, so a paused or limited provider holds back only
///   its own refreshes, and one job per entry, so an entry named by many series
///   is fetched once. The core takes the entry's lock and skips one no longer
///   due unless forced. Every refresh is stamped on the entry's row, found or not,
///   so one the source does not have waits out the provider's miss window. An
///   entry found has its images queued as a series refresh queues them.
/// </remarks>
/// <typeparam name="TProvider">The provider to refresh from.</typeparam>
[DatabaseRequired]
[MetadataProviderJob]
[JobKeyGroup(JobKeyGroup.Metadata)]
[JobPriority(Default = 10, Prioritized = 60)]
public class RefreshMetadataEntityJob<TProvider>(
    IMetadataProviderManager providerManager,
    MetadataEntityRefreshScheduler entityScheduler,
    IMetadataRefreshState refreshState,
    MetadataEntryLocks entryLocks,
    MetadataProviderScheduler providerScheduler,
    MetadataImageContributorScheduler contributorScheduler,
    ISettingsProvider settingsProvider,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob, IMetadataEntityRefreshJob where TProvider : class, IMetadataEntityProvider
{
    #region Properties

    private MetadataProviderInfo? _providerInfo;

    /// <summary>
    ///   The creator, character, studio or network to refresh, as its
    ///   <see cref="MetadataGuid"/> string.
    /// </summary>
    public string EntityID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether to refresh it however fresh it is.
    /// </summary>
    public bool Force { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Refresh Metadata Entity";

    /// <inheritdoc />
    public override string Title => "Refreshing Metadata Entity";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
        => new Dictionary<string, object> { ["Provider"] = _providerInfo?.Name ?? typeof(TProvider).Name }.WithEntry(EntityID, _providerInfo?.Source);

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
        if (!MetadataGuid.TryParse(EntityID, out var entity) || entity.Source != info.Source ||
            !MetadataEntityRefreshScheduler.EntityKinds.Contains(entity.EntityType))
        {
            _logger.LogWarning("Not refreshing {EntityID}, which is not a creator, character, studio or network on {Source}.", EntityID, info.Source);
            return;
        }

        if (!info.EnabledEntityTypes.Contains(entity.EntityType))
        {
            _logger.LogDebug("Not refreshing {Entity}: {Provider} is not enabled for {Kind}.", entity, info.Name, entity.EntityType);
            return;
        }

        var token = cancellationAccessor.Token;
        using var entityLock = await entryLocks.Acquire(entity, token).ConfigureAwait(false);

        // Checked under the lock, as another job may have refreshed it first.
        var now = DateTime.Now;
        var stored = Force ? null : entityScheduler.GetRow(entity);
        if (stored is not null && !entityScheduler.IsDue(stored, provider, now))
        {
            _logger.LogDebug("Not refreshing {Entity}, which {Provider} refreshed recently.", entity, info.Name);
            return;
        }

        // A forced run is a re-run, so only a run that read the row may call it new.
        var isNew = !Force && stored is null or { LastRefreshedAt: null, LastUpdatedAt: null };

        bool found;
        using (entryLocks.MarkUpdating(entity))
            found = await provider.RefreshEntity(entity, token).ConfigureAwait(false);

        // A miss counts as a refresh too, so it waits out the window before
        // it is asked for again.
        refreshState.RecordRefresh(entity, DateTime.Now);

        if (!found)
        {
            _logger.LogDebug("{Provider} does not have {Entity}.", info.Name, entity);
            return;
        }

        _logger.LogDebug("{Provider} refreshed {Entity}.", info.Name, entity);
        await ScheduleImages(info, entity, isNew).ConfigureAwait(false);
    }

    /// <summary>
    ///   Queues the images of an entry found: the provider's image job, or
    ///   the image contributors' jobs when that one does not run. A failure
    ///   to queue is logged, as the refresh already happened.
    /// </summary>
    /// <param name="info">The provider's info.</param>
    /// <param name="entity">The entry.</param>
    /// <param name="isNew">Whether the entry was never refreshed before, so its images are new.</param>
    /// <returns>A task that completes once the jobs are queued.</returns>
    private async Task ScheduleImages(MetadataProviderInfo info, MetadataGuid entity, bool isNew)
    {
        var token = cancellationAccessor.Token;
        var ownerImages = info.Provider is IMetadataImageProvider &&
            settingsProvider.GetSettings().Metadata.GetImageSettings(info.Source).AnyEnabled;
        try
        {
            if (!ownerImages || !await providerScheduler.ScheduleImages(info, entity, cancellationToken: token, isNew: isNew).ConfigureAwait(false))
                await contributorScheduler.ScheduleForEntry(entity, isNew: isNew, cancellationToken: token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unable to queue the images of {Entity} after refreshing it.", entity);
        }
    }

    #endregion
}
