using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Removes the creators, characters, studios and networks of the plugin
///   sources that nothing has used for longer than the settings allow. Runs
///   daily.
/// </summary>
/// <param name="purgeService">Does the purge.</param>
/// <param name="cancellationAccessor">Cancels the work.</param>
/// <param name="progressAccessor">Takes how far the work is.</param>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Metadata)]
public class PurgeOrphanedMetadataJob(
    IMetadataPurgeService purgeService,
    IJobCancellationAccessor cancellationAccessor,
    IJobProgressAccessor progressAccessor
) : BaseJob
{
    #region Properties

    /// <inheritdoc />
    public override string TypeName => "Purge Orphaned Metadata";

    /// <inheritdoc />
    public override string Title => "Purging Orphaned Metadata";

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        var removed = await purgeService.PurgeOrphaned(null, null, progressAccessor.Progress, cancellationAccessor.Token).ConfigureAwait(false);
        _logger.LogDebug("Purged {Count} orphaned people, studios and networks of the plugin sources.", removed);
    }

    #endregion
}
