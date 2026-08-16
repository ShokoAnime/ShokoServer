using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Fetch unread notifications and messages from AniDB.
/// </summary>
public sealed class GetAnidbNotificationsAction(IQueueScheduler scheduler) : IExecutableAction
{
    public string Name => "Get AniDB Notifications";

    public string? Description => "Fetch unread notifications and messages from AniDB.";

    public ActionCategory Category => ActionCategory.AniDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<CheckAniDBNotificationsJob>(c => c.ForceRefresh = true, ct: token);
}
