using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Airing;

namespace Shoko.Server.Actions;

/// <summary>
///   Queue a chunk of the airing schedule sweep of every provider that is due one.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class SweepAiringSchedulesAction(IQueueScheduler scheduler) : QueueJobScheduledAction<SweepAiringSchedulesJob>(scheduler)
{
    public override string Name => "Sweep Airing Schedules";

    public override string? Description => "Queues the next chunk of the airing schedule sweep of every airing schedule provider that is due one.";

    public override ActionCategory Category => ActionCategory.Miscellaneous;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromMinutes(15))];
}
