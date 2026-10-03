using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the people, studios and networks of the plugin sources that
///   nothing has used for longer than the metadata settings allow.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class PurgeExpiredOrphanedMetadataAction(IQueueScheduler scheduler) : QueueJobScheduledAction<PurgeOrphanedMetadataJob>(scheduler)
{
    public override string Name => "Purge Expired Orphaned Metadata";

    public override string? Description => "Removes the people, studios and networks of the plugin metadata sources that nothing has used for longer than the metadata settings allow.";

    public override ActionCategory Category => ActionCategory.Maintenance;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
