using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Read the media info of the files without it, or with media info from an
///   older version. Runs at start-up by default.
/// </summary>
/// <remarks>
///   A run is the job queuing a media info job per such file, so it shows that
///   job's progress and an admin can cancel it. The media info jobs run on
///   their own.
/// </remarks>
/// <param name="scheduler">The queue.</param>
public sealed class UpdateMissingMediaInfoAction(IQueueScheduler scheduler) : QueueJobScheduledAction<MediaInfoMissingFilesJob>(scheduler)
{
    public override string Name => "Update Missing Media Info";

    public override string? Description => "Read the media info of the files without it, or with media info from an older version.";

    public override ActionCategory Category => ActionCategory.Maintenance;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.AtStartup];
}
