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

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to sync local state with the AniDB MyList for all series? This may take a while.";

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<SyncAniDBMyListJob>(j => j.ForceRefresh = true, ct: token);
}
