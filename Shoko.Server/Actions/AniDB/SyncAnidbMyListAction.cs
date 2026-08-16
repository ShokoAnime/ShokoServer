using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync all local state to the AniDB MyList. This overwrites AniDB data.
/// </summary>
public sealed class SyncAnidbMyListAction(IQueueScheduler scheduler) : IExecutableAction
{
    public string Name => "Sync AniDB MyList";

    public string? Description => "Sync all local state to the AniDB MyList. This can overwrite AniDB data irreversibly.";

    public ActionCategory Category => ActionCategory.AniDB;

    // The legacy SyncMyList endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<SyncAniDBMyListJob>(j => j.ForceRefresh = true, ct: token);
}
