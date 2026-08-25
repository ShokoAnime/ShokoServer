using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

/// <summary>
/// Fires the recurring MyList sync. It exists purely so the sync itself is
/// always enqueued through <see cref="IMyListService.ScheduleSync(MyListSyncOptions, bool)"/>, which
/// resolves the sync options against the settings up front. The recurring
/// registration carries no job data of its own, so without this the sync would
/// be enqueued with its non-nullable options left at their defaults, silently
/// overriding whatever the user had configured.
/// </summary>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class SyncAniDBMyListRecurringJob(IMyListService mylistService) : BaseJob
{
    public override string TypeName => "Start Recurring AniDB MyList Sync";

    public override string Title => "Starting Recurring AniDB MyList Sync";

    public override Dictionary<string, object> Details => [];

    public override Task Execute()
        => mylistService.ScheduleSync();
}
