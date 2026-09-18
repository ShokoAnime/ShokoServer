using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// The daily retention sweep: a schedule and its airings go a year after the
/// run ended, not after it started, so a finished cour keeps its whole history
/// until the lot ages out.
/// </summary>
/// <remarks>
/// It runs on a timer rather than off a provider's writes, so schedules left
/// behind by an uninstalled provider age out too.
/// </remarks>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Airing)]
public class AiringScheduleRetentionJob(IAiringScheduleService airingScheduleService) : BaseJob
{
    private readonly AiringScheduleService _airingScheduleService = (AiringScheduleService)airingScheduleService;

    /// <inheritdoc/>
    public override string TypeName => "Remove Aged Out Airing Schedules";

    /// <inheritdoc/>
    public override string Title => "Removing Aged Out Airing Schedules";

    /// <inheritdoc/>
    public override Task Execute()
    {
        _airingScheduleService.RunRetentionSweep();
        return Task.CompletedTask;
    }
}
