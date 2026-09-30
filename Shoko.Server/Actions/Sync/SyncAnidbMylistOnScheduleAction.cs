using System;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync the AniDB MyList with the local collection, as the MyList settings say.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class SyncAnidbMylistOnScheduleAction(IQueueScheduler scheduler) : QueueJobScheduledAction<SyncAniDBMylistRecurringJob>(scheduler)
{
    public override string Name => "Sync AniDB MyList On Schedule";

    public override string? Description => "Syncs the AniDB MyList with the local collection as the MyList settings say, reusing a MyList downloaded in the last few hours.";

    public override ActionCategory Category => ActionCategory.Sync;

    public override TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public override bool ScheduleCountsManualRuns => true;
}
