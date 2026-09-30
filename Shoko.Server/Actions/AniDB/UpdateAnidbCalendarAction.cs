using System;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Update the AniDB calendar data for use on the dashboard.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class UpdateAnidbCalendarAction(IQueueScheduler scheduler) : QueueJobScheduledAction<GetAniDBCalendarJob>(scheduler)
{
    public override string Name => "Update AniDB Calendar";

    public override string? Description => "Update the AniDB calendar data for use on the dashboard.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override TimeSpan? MinimumInterval => TimeSpan.FromHours(4);

    public override bool ScheduleCountsManualRuns => true;
}
