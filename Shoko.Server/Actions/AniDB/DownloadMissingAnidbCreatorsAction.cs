using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Download all missing AniDB creator data via the UDP API.
/// </summary>
public sealed class DownloadMissingAnidbCreatorsAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Download Missing AniDB Creators";

    public string? Description => "Download all missing or incomplete AniDB creator data via the UDP API.";

    public ActionCategory Category => ActionCategory.AniDB;

    // The legacy DownloadMissingAniDBCreators endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.ScheduleMissingAnidbCreators();
}
