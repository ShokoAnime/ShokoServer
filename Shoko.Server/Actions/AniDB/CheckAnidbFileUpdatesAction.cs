using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Search again for the releases of files with no episode, and queue the anime files need.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class CheckAnidbFileUpdatesAction(IQueueScheduler scheduler) : QueueJobScheduledAction<CheckAniDBFileUpdatesJob>(scheduler)
{
    public override string Name => "Check AniDB File Updates";

    public override string? Description => "Searches again for the releases of files with no episode, and queues the anime that files are linked to but that are missing.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public override bool ScheduleCountsManualRuns => true;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
