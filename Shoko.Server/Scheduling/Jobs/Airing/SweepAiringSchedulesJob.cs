using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// The sweep dispatcher: it works out which sweeping providers are due a chunk
/// and queues one each. It never sweeps anything itself, so a slow provider
/// can't hold up the tick or another provider's turn.
/// </summary>
/// <remarks>
/// It ticks far more often than any provider's own interval, because a sweep in
/// progress is picked up on whichever tick comes next. The chunks it queues are
/// keyed per provider, so a tick landing on a provider that is still working is
/// a no-op for that one.
/// </remarks>
[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Airing)]
public class SweepAiringSchedulesJob(IAiringScheduleService airingScheduleService) : BaseJob
{
    private readonly AiringScheduleService _airingScheduleService = (AiringScheduleService)airingScheduleService;

    /// <inheritdoc/>
    public override string TypeName => "Dispatch Airing Schedule Sweeps";

    /// <inheritdoc/>
    public override string Title => "Dispatching Airing Schedule Sweeps";

    /// <inheritdoc/>
    public override Task Execute()
        => _airingScheduleService.ScheduleSweeps();
}
