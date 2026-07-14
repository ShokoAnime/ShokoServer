using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Fetch unread notifications and messages from AniDB.
/// </summary>
internal sealed class GetAnidbNotificationsAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Get AniDB Notifications";
    public string? Description => "Fetch unread notifications and messages from AniDB.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.GetAniDBNotifications();
}
