using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Force a complete redownload of TMDB images for the series.
/// </summary>
public sealed class UpdateTmdbImagesForceSeriesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Update TMDB Images - Force";

    public string? Description => "Forces a complete redownload of images from TMDB.";

    public ActionCategory Category => ActionCategory.TMDB;

    // The legacy TMDB/Movie|Show/Action/DownloadImages endpoints were admin-gated; keep Admin-level so the permission surface does not widen.
    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var xref in Series.TmdbShowCrossReferences)
            await scheduler.Enqueue<DownloadTmdbShowImagesJob>(j =>
            {
                j.TmdbShowID = xref.TmdbShowID;
                j.ForceDownload = true;
            }, ct: token);
        foreach (var xref in Series.TmdbMovieCrossReferences)
            await scheduler.Enqueue<DownloadTmdbMovieImagesJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceDownload = true;
            }, ct: token);
    }
}
