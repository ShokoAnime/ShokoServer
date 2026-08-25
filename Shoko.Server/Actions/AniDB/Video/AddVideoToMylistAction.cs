using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Add the file to the user's AniDB MyList. A file already on the MyList has
///   its state refreshed rather than being added twice.
/// </summary>
public sealed class AddVideoToMylistAction(IMylistService mylistService) : VideoAction
{
    public override string Name => "Add to Mylist";

    public override string? Description => "Adds the file to your AniDB Mylist.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => mylistService.ScheduleAddVideo(Video);
}
