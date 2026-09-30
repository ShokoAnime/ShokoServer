using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the stored series, films and collections nothing has linked to for longer than the metadata settings allow.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class PurgeExpiredUnusedMetadataAction(IQueueScheduler scheduler) : QueueJobScheduledAction<PurgeUnusedMetadataJob>(scheduler)
{
    public override string Name => "Purge Expired Unused Metadata";

    public override string? Description => "Removes the series, films and collections of TMDB and the plugin metadata sources that nothing has linked to for longer than the metadata settings allow.";

    public override ActionCategory Category => ActionCategory.Maintenance;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
