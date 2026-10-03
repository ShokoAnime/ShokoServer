using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan every file in the drop source folders, then import what changed.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class ScanDropFoldersAction(IQueueScheduler scheduler) : QueueJobScheduledAction<ScanDropFoldersJob>(scheduler)
{
    public override string Name => "Scan Drop Folders";

    public override string? Description => "Scan every file in the drop source folders, hash the new or changed ones, and find their releases.";

    public override ActionCategory Category => ActionCategory.Import;
}
