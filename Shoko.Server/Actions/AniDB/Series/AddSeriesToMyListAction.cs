using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Add every file in the series to the user's AniDB MyList. Files already on the
///   MyList have their state refreshed rather than being added twice.
/// </summary>
public sealed class AddSeriesToMyListAction(IMyListService myListService) : SeriesAction
{
    public override string Name => "Add to MyList";

    public override string? Description => "Adds every file in the series to your AniDB MyList.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var video in MyListActionScope.VideosOf(Series))
            await myListService.ScheduleAddVideo(video);
    }
}
