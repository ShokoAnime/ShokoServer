using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Recalculate stats for all series and re-apply group filters.
/// </summary>
public sealed class UpdateSeriesStatsAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Update Series Stats";

    public string? Description => "Recalculate statistics for all series and re-apply group filters.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.UpdateAllStats();
}
