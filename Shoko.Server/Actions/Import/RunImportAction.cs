using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Run the full import pipeline: scan for new files, hash them, find
///   releases, update metadata, and download missing images.
/// </summary>
public sealed class RunImportAction(IQueueScheduler scheduler) : IScheduledAction
{
    public string Name => "Run Import";

    public string? Description => "Check for new files, hash them, scan for metadata matches, and download missing images.";

    public ActionCategory Category => ActionCategory.Import;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => scheduler.Enqueue<ImportJob>(ct: token);
}
