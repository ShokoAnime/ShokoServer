using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Check whether the server can reach the internet.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class CheckNetworkAvailabilityAction(IQueueScheduler scheduler) : QueueJobScheduledAction<CheckNetworkAvailabilityJob>(scheduler)
{
    public override string Name => "Check Network Availability";

    public override string? Description => "Checks whether the server can reach the internet. Jobs that need the network wait until it can.";

    public override ActionCategory Category => ActionCategory.Miscellaneous;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.AtStartup, ActionTrigger.Every(TimeSpan.FromMinutes(30))];
}
