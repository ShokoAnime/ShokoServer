using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Recalculate stats for all series and re-apply group filters.
/// </summary>
internal sealed class UpdateSeriesStatsAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Update Series Stats";
    public string? Description => "Recalculate statistics for all series and re-apply group filters.";
    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.UpdateAllStats();
    }
}
