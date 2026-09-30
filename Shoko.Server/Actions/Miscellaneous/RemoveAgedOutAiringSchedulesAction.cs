using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Airing;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove the airing schedules whose run ended more than a year ago, with their airings.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class RemoveAgedOutAiringSchedulesAction(IQueueScheduler scheduler) : QueueJobScheduledAction<AiringScheduleRetentionJob>(scheduler)
{
    public override string Name => "Remove Aged Out Airing Schedules";

    public override string? Description => "Removes the airing schedules whose run ended more than a year ago, with their airings.";

    public override ActionCategory Category => ActionCategory.Miscellaneous;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
