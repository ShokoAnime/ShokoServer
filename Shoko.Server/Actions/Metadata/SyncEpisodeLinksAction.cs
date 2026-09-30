using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Actions;

/// <summary>
///   Copy the seasons and numbers of the stored episodes of every plugin source onto the episode links naming them.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class SyncEpisodeLinksAction(IQueueScheduler scheduler) : QueueJobScheduledAction<SyncEpisodeLinksJob>(scheduler)
{
    public override string Name => "Sync Episode Links";

    public override string? Description => "Copies the seasons and numbers of the stored episodes of every plugin metadata source onto the episode links naming them.";

    public override ActionCategory Category => ActionCategory.Maintenance;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
