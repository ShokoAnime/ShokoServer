using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh all AniDB anime info from the remote API.
/// </summary>
public sealed class UpdateAllAnidbInfoAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Update All AniDB Info";

    public string? Description => "Refresh all AniDB anime information from the remote API.";

    public ActionCategory Category => ActionCategory.AniDB;

    // The legacy UpdateAllAniDBInfo endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.RunImport_UpdateAllAniDB();
}
