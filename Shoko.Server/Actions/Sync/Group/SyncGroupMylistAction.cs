using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Reconcile the AniDB MyList entries covering every file in the group against the
///   local state — adding what is missing and syncing watched and storage
///   states in both directions, as the full sync would.
/// </summary>
public sealed class SyncGroupMylistAction(IMylistService mylistService, IActorContext actorContext) : GroupAction, IVisibleSeriesGroupAction
{
    public override string Name => "Sync MyList";

    public override string? Description => "Reconciles your AniDB MyList with the local state for every file in the group.";

    public override ActionCategory Category => ActionCategory.Sync;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => mylistService.ScheduleSync(Group.GetVisibleSeries(actorContext).SelectMany(series => ((IShokoSeries)series).Videos).DistinctBy(video => video.LocalID));
}
