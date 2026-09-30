using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync the plugin repositories, and upgrade the plugins when automatic upgrades are on.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class CheckPluginUpdatesAction(IQueueScheduler scheduler) : QueueJobScheduledAction<CheckPluginUpdatesJob>(scheduler)
{
    public override string Name => "Check Plugin Updates";

    public override string? Description => "Syncs the plugin repositories, and upgrades the plugins when automatic upgrades are on. Does nothing while automatic syncing is off.";

    public override ActionCategory Category => ActionCategory.Maintenance;

    public override TimeSpan? MinimumInterval => TimeSpan.FromHours(1);

    public override bool ScheduleCountsManualRuns => true;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(6))];
}
