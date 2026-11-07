using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Actions;

/// <summary>
///   Judge again, by the matching rules as they are now, the series and movie
///   links made on dates alone, and remove the ones they no longer take.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class RejudgeMetadataLinksAction(IQueueScheduler scheduler) : QueueJobScheduledAction<RejudgeMetadataLinksJob>(scheduler)
{
    public override string Name => "Re-judge Date-Only Metadata Links";

    public override string? Description => "Judges the series and movie links of every metadata source that were made on dates alone again, by the matching rules as they are now. Links the rules no longer take are removed and their anime searched again; links verified by a user, or with title evidence, are left alone.";

    public override ActionCategory Category => ActionCategory.Destructive;

}
