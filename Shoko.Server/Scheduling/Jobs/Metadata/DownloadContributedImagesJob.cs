using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Links one image contributor's images to an entry of another source and
///   everything under it, and schedules the downloads the image settings ask
///   for.
/// </summary>
/// <remarks>
///   One job type per contributor, so each runs in a pool of its own. It walks
///   the entry as the owner's image job does, under the same locks, and hands
///   each enabled entity's images to <see cref="MetadataImageReconciler"/>
///   under the contributor's own source and image settings. An entry that is
///   gone is skipped quietly; one entity failing does not stop the rest, but
///   the job fails afterwards so the queue retries it.
/// </remarks>
/// <typeparam name="TContributor">The contributor to ask.</typeparam>
[DatabaseRequired]
[NetworkRequired]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Metadata)]
// Ranked as queued by JobPriorities.ForMetadataEntry: a new entry adds 50 on top of prioritized = default + 50.
public class DownloadContributedImagesJob<TContributor>(
    IMetadataImageContributorManager contributorManager,
    IMetadataService metadataService,
    MetadataImageReconciler reconciler,
    MetadataEntryLocks entryLocks,
    ISettingsProvider settingsProvider,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob, IContributedImagesJob where TContributor : class, IMetadataImageContributor
{
    #region Properties

    private MetadataImageContributorInfo? _contributorInfo;

    /// <summary>
    ///   The series, film or collection whose entities get the images, as its
    ///   <see cref="MetadataGuid"/> string.
    /// </summary>
    public string EntryID { get; set; } = string.Empty;

    /// <summary>
    ///   Whether to download the desired images again even when they are
    ///   there.
    /// </summary>
    public bool Force { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Download Contributed Images";

    /// <inheritdoc />
    public override string Title => "Downloading Contributed Images";

    /// <inheritdoc />
    public override Dictionary<string, object> Details
        => new Dictionary<string, object> { ["Contributor"] = _contributorInfo?.Name ?? typeof(TContributor).Name }.WithEntry(EntryID);

    /// <inheritdoc />
    public override void PostInit()
        => _contributorInfo = contributorManager.ImageContributors.FirstOrDefault(info => info.Contributor.GetType() == typeof(TContributor));

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        if (contributorManager.ImageContributors.FirstOrDefault(info => info.Contributor.GetType() == typeof(TContributor)) is not { } info)
        {
            _logger.LogDebug("Not downloading images from {Contributor}, which is not registered.", typeof(TContributor).Name);
            return;
        }

        if (!MetadataGuid.TryParse(EntryID, out var entry))
        {
            _logger.LogWarning("Not downloading images from {Contributor} for {EntryID}, which is not an entry.", info.Name, EntryID);
            return;
        }

        if (!MetadataImageContributorScheduler.GetContributorsCovering(contributorManager, entry).Contains(info))
        {
            _logger.LogDebug("Not downloading images for {Entry}: {Contributor} is not enabled for it.", entry, info.Name);
            return;
        }

        var settings = settingsProvider.GetSettings().Metadata.GetImageSettings(info.Source);
        if (!settings.AnyEnabled)
        {
            _logger.LogDebug("Not downloading images for {Entry}: no image type is downloaded for {Source}.", entry, info.Source);
            return;
        }

        // Read under the locks so no purge (or plugin refresh) removes the entities mid-job.
        var token = cancellationAccessor.Token;
        using var entryLock = entry.Source.IsCore ? null : await entryLocks.Acquire(entry, token).ConfigureAwait(false);
        using var imagesLock = await entryLocks.AcquireImages(entry, token).ConfigureAwait(false);
        var (entities, originalLanguage) = MetadataImageEntities.Gather(metadataService, entry);
        if (entities.Count is 0)
        {
            _logger.LogDebug("Not downloading images from {Contributor} for {Entry}, which is gone.", info.Name, entry);
            return;
        }

        var failures = new List<Exception>();
        foreach (var entity in entities)
        {
            token.ThrowIfCancellationRequested();
            if (!info.EnabledScope.Contains(entity.ID) || !settings.AnyEnabledFor(entity.ID.EntityType))
                continue;

            try
            {
                var candidates = await info.Contributor.GetImages(entity, token).ConfigureAwait(false);
                if (candidates is null)
                    continue;

                var linked = await reconciler.Reconcile(
                    entity,
                    info.Source,
                    candidates,
                    settings,
                    originalLanguage,
                    Force,
                    entityLocked: entryLock is not null && entity.ID == entry,
                    cancellationToken: token
                ).ConfigureAwait(false);
                _logger.LogTrace("Linked {Count} images from {Contributor} to {Entity}.", linked, info.Name, entity.ID);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Contributor} failed to give the images of {Entity}.", info.Name, entity.ID);
                failures.Add(ex);
            }
        }

        if (failures.Count is 1)
            throw failures[0];
        if (failures.Count > 1)
            throw new AggregateException($"Downloading images from {info.Name} failed for {failures.Count} entities.", failures);
    }

    #endregion
}
