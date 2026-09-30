using System;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Ask AniDB which anime changed since the last time, and update those in the collection.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class GetUpdatedAnidbAnimeAction(IQueueScheduler scheduler) : QueueJobScheduledAction<GetUpdatedAniDBAnimeJob>(scheduler)
{
    public override string Name => "Get Updated AniDB Anime";

    public override string? Description => "Asks AniDB which anime changed since the last time, and queues an update of those in the collection.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override TimeSpan? MinimumInterval => TimeSpan.FromHours(4);

    public override bool ScheduleCountsManualRuns => true;
}
