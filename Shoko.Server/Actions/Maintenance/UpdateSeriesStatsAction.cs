using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Recalculate stats for all series and re-apply group filters.
/// </summary>
public sealed class UpdateSeriesStatsAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Update Series Stats";

    public string? Description => "Recalculate statistics for all series and re-apply group filters.";

    public ActionCategory Category => ActionCategory.Maintenance;

    // Matches today: [Authorize("admin")] on the legacy UpdateSeriesStats endpoint.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.UpdateAllStats();
}
