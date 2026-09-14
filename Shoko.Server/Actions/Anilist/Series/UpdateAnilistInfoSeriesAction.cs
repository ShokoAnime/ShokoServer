using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Get the latest series information from AniList.
/// </summary>
public sealed class UpdateAnilistInfoSeriesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Update AniList Info";

    public override string? Description => "Gets the latest series information from AniList.";

    public override ActionCategory Category => ActionCategory.AniList;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var xref in Series.AnilistAnimeCrossReferences)
            await scheduler.Enqueue<UpdateAnilistAnimeJob>(j =>
            {
                j.AnilistAnimeID = xref.AnilistAnimeID;
                j.ForceRefresh = false;
                j.DownloadImages = true;
            }, ct: token);
    }
}
