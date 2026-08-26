using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Add every file for the episode to the user's AniDB MyList. Files already on the
///   MyList have their state refreshed rather than being added twice.
/// </summary>
public sealed class AddEpisodeToMylistAction(IMylistService mylistService) : EpisodeAction
{
    public override string Name => "Add to MyList";

    public override string? Description => "Adds every file for the episode to your AniDB MyList.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var video in MylistActionScope.VideosOf(Episode))
            await mylistService.ScheduleAddVideo(video);
    }
}
