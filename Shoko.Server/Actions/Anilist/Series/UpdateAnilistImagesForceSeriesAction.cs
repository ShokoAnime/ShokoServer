using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Force a complete redownload of images from AniList.
/// </summary>
public sealed class UpdateAnilistImagesForceSeriesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Update AniList Images - Force";

    public override string? Description => "Forces a complete redownload of images from AniList.";

    public override ActionCategory Category => ActionCategory.AniList;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var xref in Series.AnilistAnimeCrossReferences)
            await scheduler.Enqueue<DownloadAnilistAnimeImagesJob>(j =>
            {
                j.AnilistAnimeID = xref.AnilistAnimeID;
                j.ForceDownload = true;
            }, ct: token);
    }
}
