using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove the API keys that have expired.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class CleanUpExpiredTokensAction(IQueueScheduler scheduler) : QueueJobScheduledAction<CleanupExpiredTokensJob>(scheduler)
{
    public override string Name => "Clean Up Expired API Keys";

    public override string? Description => "Removes the API keys that have expired.";

    public override ActionCategory Category => ActionCategory.Maintenance;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
