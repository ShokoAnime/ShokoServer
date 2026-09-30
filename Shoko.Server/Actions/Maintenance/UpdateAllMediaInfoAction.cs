using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Update media info for all files in the collection.
/// </summary>
public sealed class UpdateAllMediaInfoAction(IQueueScheduler scheduler) : IScheduledAction
{
    public string Name => "Update All Media Info";

    public string? Description => "Re-read and update media info for all files in the collection.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => scheduler.Enqueue<MediaInfoAllFilesJob>(ct: token);
}
