using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Links one provider's images to a stored entry and everything under it,
///   and schedules the downloads the image settings ask for.
/// </summary>
/// <remarks>
///   One job type per provider, so a paused or limited provider holds back only
///   its own image jobs. It holds the entry's image lock (and outside the core's
///   own sources the entry's lock), so no purge or refresh removes what it links. A failed entity
///   does not stop the rest, but the job fails afterwards so the queue retries it.
///   The enabled contributors are queued last, even when the provider was not asked.
/// </remarks>
/// <typeparam name="TProvider">The provider to ask.</typeparam>
[DatabaseRequired]
[MetadataProviderJob]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Metadata)]
public class DownloadMetadataImagesJob<TProvider>(
    IMetadataProviderManager providerManager,
    IMetadataService metadataService,
    MetadataImageReconciler reconciler,
    MetadataEntryLocks entryLocks,
    ISettingsProvider settingsProvider,
    MetadataImageContributorScheduler contributorScheduler,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob, IMetadataImagesJob where TProvider : class, IMetadataImageProvider
{
    #region Properties

    private MetadataProviderInfo? _providerInfo;

    /// <summary>
    ///   The series, film, collection, creator, character, studio or network
    ///   whose images are linked, as its <see cref="MetadataGuid"/> string.
    /// </summary>
    public string EntryID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether to download the desired images again even when they are
    ///   there.
    /// </summary>
    public bool Force { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Download Metadata Images";

    /// <inheritdoc />
    public override string Title => "Downloading Metadata Images";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
        => new Dictionary<string, object> { ["Provider"] = _providerInfo?.Name ?? typeof(TProvider).Name }.WithEntry(EntryID);

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
        var source = info.Source;
        if (!MetadataGuid.TryParse(EntryID, out var entry) || entry.Source != source)
        {
            _logger.LogWarning("Not downloading images for {EntryID}, which is not an entry on {Source}.", EntryID, source);
            return;
        }

        // The contributors are queued even when the owner is not asked, as
        // they keep their own switches and image settings.
        var ownerAsked = false;
        var settings = settingsProvider.GetSettings().Image.GetMetadataSourceSettings(source);
        if (!info.Enabled)
        {
            _logger.LogDebug("Not downloading images from {Provider}, which is disabled.", info.Name);
        }
        else if (!MetadataProviderScheduler.MayRefresh(info, entry.EntityType))
        {
            _logger.LogDebug("Not downloading images for {Entry}: {Provider} is not enabled for it.", entry, info.Name);
        }
        else if (!settings.AnyEnabled)
        {
            _logger.LogDebug("Not downloading images for {Entry}: no image type is downloaded for {Source}.", entry, source);
        }
        else
        {
            ownerAsked = true;
        }

        // Read under the locks so no purge (or plugin refresh) removes the entities mid-job.
        // The entry's lock comes first, in the order a purge takes them.
        var token = cancellationAccessor.Token;
        using var entryLock = source.IsCore ? null : await entryLocks.Acquire(entry, token).ConfigureAwait(false);
        using var imagesLock = await entryLocks.AcquireImages(entry, token).ConfigureAwait(false);
        var (entities, originalLanguage) = MetadataImageEntities.Gather(metadataService, entry);
        if (entities.Count is 0)
        {
            _logger.LogDebug("Not downloading images for {Entry}, which is not stored.", entry);
            return;
        }

        if (!ownerAsked)
        {
            await contributorScheduler.ScheduleForEntry(entry, Force, cancellationToken: token).ConfigureAwait(false);
            return;
        }

        var failures = new List<Exception>();
        foreach (var entity in entities)
        {
            token.ThrowIfCancellationRequested();
            if (!settings.AnyEnabledFor(entity.ID.EntityType))
                continue;

            try
            {
                var candidates = await provider.GetImages(entity.ID, token).ConfigureAwait(false);
                if (candidates is null)
                    continue;

                var linked = await reconciler.Reconcile(
                    entity,
                    source,
                    candidates,
                    settings,
                    originalLanguage,
                    Force,
                    entityLocked: entryLock is not null && entity.ID == entry,
                    cancellationToken: token
                ).ConfigureAwait(false);
                _logger.LogTrace("Linked {Count} images from {Provider} to {Entity}.", linked, info.Name, entity.ID);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Provider} failed to give the images of {Entity}.", info.Name, entity.ID);
                failures.Add(ex);
            }
        }

        // The contributors add theirs once the owner's are linked, whether or
        // not every entity got its images.
        await contributorScheduler.ScheduleForEntry(entry, Force, cancellationToken: token).ConfigureAwait(false);

        if (failures.Count is 1)
            throw failures[0];
        if (failures.Count > 1)
            throw new AggregateException($"Downloading images from {info.Name} failed for {failures.Count} entities.", failures);
    }

    #endregion
}
