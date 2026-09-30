using System;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Fetch unread notifications and messages from AniDB.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class GetAnidbNotificationsAction(IQueueScheduler scheduler) : QueueJobScheduledAction<CheckAniDBNotificationsJob>(scheduler)
{
    public override string Name => "Get AniDB Notifications";

    public override string? Description => "Fetch unread notifications and messages from AniDB.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public override bool ScheduleCountsManualRuns => true;
}
