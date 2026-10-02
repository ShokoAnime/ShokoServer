using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Add every file in the group to the user's AniDB MyList. Files already on the
///   MyList have their state refreshed rather than being added twice.
/// </summary>
public sealed class AddGroupToMylistAction(IMylistService mylistService, IActorContext actorContext) : GroupAction, IVisibleSeriesGroupAction
{
    public override string Name => "Add to MyList";

    public override string? Description => "Adds every file in the group to your AniDB MyList.";

    public override ActionCategory Category => ActionCategory.Sync;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var video in Group.GetVisibleSeries(actorContext).SelectMany(series => ((IShokoSeries)series).Videos).DistinctBy(video => video.LocalID))
            await mylistService.ScheduleAddVideo(video);
    }
}
