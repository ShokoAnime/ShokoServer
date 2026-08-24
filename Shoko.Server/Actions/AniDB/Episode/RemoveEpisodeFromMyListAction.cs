using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Dispose of the AniDB MyList entries covering every file for the episode, applying
///   the configured delete type — which may mark the entries rather than
///   remove them outright.
/// </summary>
public sealed class RemoveEpisodeFromMyListAction(IMyListService myListService) : EpisodeAction
{
    public override string Name => "Remove from MyList";

    public override string? Description => "Removes every file for the episode from your AniDB MyList, following your configured delete type.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => "Are you sure you want to remove every file for this episode from your AniDB MyList?";

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var video in MyListActionScope.VideosOf(Episode))
            await myListService.ScheduleDisposeVideo(video);
    }
}
