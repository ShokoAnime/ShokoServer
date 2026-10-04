using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   Queues the image contributors' jobs for an entry whose images the core
///   refreshes.
/// </summary>
/// <param name="contributorManager">The registered contributors.</param>
/// <param name="scheduler">The queue.</param>
/// <param name="logger">Where failures to queue are reported.</param>
public class MetadataImageContributorScheduler(
    IMetadataImageContributorManager contributorManager,
    IQueueScheduler scheduler,
    ILogger<MetadataImageContributorScheduler> logger
)
{
    #region Scheduling

    /// <summary>
    ///   Queue one image job for each contributor enabled for an entry or
    ///   anything the image walk reaches under it.
    /// </summary>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="force">Whether to download the desired images again even when they are there.</param>
    /// <param name="prioritize">Whether to queue them ahead of the rest even though they are not forced.</param>
    /// <param name="isNew">Whether this is the entry's first image run, after it was just linked or created.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many jobs were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <c>null</c>.</exception>
    public async Task<int> ScheduleForEntry(
        MetadataGuid entryID,
        bool force = false,
        bool prioritize = false,
        bool isNew = false,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entryID);
        var queued = 0;
        foreach (var info in GetContributorsCovering(contributorManager, entryID))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MetadataImageContributorJobs.GetJobType(info.Contributor.GetType()) is not { } jobType)
                continue;

            try
            {
                await scheduler.EnqueueWithPriority(
                    jobType,
                    job =>
                    {
                        var images = (IContributedImagesJob)job;
                        images.EntryID = entryID.ToString();
                        images.Force = force;
                    },
                    JobPriorities.ForMetadataEntry(entryID.EntityType, isNew, force || prioritize)
                ).ConfigureAwait(false);
                queued++;
            }
            catch (Exception ex)
            {
                // One contributor failing to queue is no reason for the rest
                // to go unasked.
                logger.LogError(ex, "Failed to queue the images from {Contributor} for {Entry}.", info.Name, entryID);
            }
        }

        return queued;
    }

    /// <summary>
    ///   The contributors enabled for an entry or anything the image walk
    ///   reaches under it on its source.
    /// </summary>
    /// <param name="contributorManager">The registered contributors.</param>
    /// <param name="entryID">The series, film or collection.</param>
    /// <returns>The contributors, in plugin load order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public static IReadOnlyList<MetadataImageContributorInfo> GetContributorsCovering(IMetadataImageContributorManager contributorManager, MetadataGuid entryID)
    {
        ArgumentNullException.ThrowIfNull(contributorManager);
        ArgumentNullException.ThrowIfNull(entryID);
        var kinds = MetadataImageEntities.GetReachableKinds(entryID.EntityType);
        return [.. contributorManager.ImageContributors.Where(info => kinds.Any(kind => info.EnabledScope.Contains(entryID.Source, kind)))];
    }

    #endregion
}
