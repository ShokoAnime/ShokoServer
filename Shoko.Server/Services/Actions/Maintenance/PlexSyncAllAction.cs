using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Sync watch states with Plex for all users with a Plex token.
/// </summary>
internal sealed class PlexSyncAllAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Plex Sync All";
    public string? Description => "Sync watch states with Plex for all users with a configured Plex token.";
    public ActionCategory Category => ActionCategory.Sync;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.PlexSyncAll();
}
