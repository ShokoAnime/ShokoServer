using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Update media info for all files in the collection.
/// </summary>
/// <remarks>
///   A run is the job queuing a media info job per file, so it shows that
///   job's progress and an admin can cancel it. The media info jobs run on
///   their own.
/// </remarks>
/// <param name="scheduler">The queue.</param>
public sealed class UpdateAllMediaInfoAction(IQueueScheduler scheduler) : QueueJobScheduledAction<MediaInfoAllFilesJob>(scheduler)
{
    public override string Name => "Update All Media Info";

    public override string? Description => "Re-read and update media info for all files in the collection.";

    public override ActionCategory Category => ActionCategory.Maintenance;
}
