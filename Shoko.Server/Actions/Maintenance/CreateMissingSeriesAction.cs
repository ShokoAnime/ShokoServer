using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Create anime series entries for files that have release info but no
///   corresponding series.
/// </summary>
/// <remarks>
///   Only queues the series creations, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class CreateMissingSeriesAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Create Missing Series";

    public string? Description => "Create series entries for files that have release info but no corresponding series.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.CreateMissingSeries(progress, token);
}
