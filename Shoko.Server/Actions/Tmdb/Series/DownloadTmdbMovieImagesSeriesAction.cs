using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Download images for all TMDB movies linked to the series.
/// </summary>
public sealed class DownloadTmdbMovieImagesSeriesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Download TMDB Movie Images";

    public string? Description => "Download any missing images for linked TMDB movies.";

    public ActionCategory Category => ActionCategory.TMDB;

    // The legacy TMDB/Movie/Action/DownloadImages endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var xref in Series.TmdbMovieCrossReferences)
            await scheduler.Enqueue<DownloadTmdbMovieImagesJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceDownload = false;
            }, ct: token);
    }
}
