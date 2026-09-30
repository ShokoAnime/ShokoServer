using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Search again for the releases whose source or languages are unknown.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class ScanForMissingReleaseInfoAction(IQueueScheduler scheduler) : QueueJobScheduledAction<ScanForMissingReleaseInfoJob>(scheduler)
{
    public override string Name => "Scan For Missing Release Info";

    public override string? Description => "Searches again for the releases of files whose source is unknown or whose audio or subtitle languages are missing, as often as each release provider allows.";

    public override ActionCategory Category => ActionCategory.Import;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
