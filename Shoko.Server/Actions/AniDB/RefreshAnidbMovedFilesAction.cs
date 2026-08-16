using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Process any pending AniDB file-moved notifications.
/// </summary>
public sealed class RefreshAnidbMovedFilesAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Refresh AniDB Moved Files";

    public string? Description => "Process pending AniDB file-moved notifications and update affected files.";

    public ActionCategory Category => ActionCategory.AniDB;

    // The legacy RefreshAniDBMovedFiles endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.RefreshAniDBMovedFiles(true);
}
