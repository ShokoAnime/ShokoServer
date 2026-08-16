using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Create anime series entries for files that have release info but no
///   corresponding series.
/// </summary>
public sealed class CreateMissingSeriesAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Create Missing Series";

    public string? Description => "Create series entries for files that have release info but no corresponding series.";

    public ActionCategory Category => ActionCategory.Maintenance;

    // The legacy CreateMissingSeries endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.CreateMissingSeries();
}
