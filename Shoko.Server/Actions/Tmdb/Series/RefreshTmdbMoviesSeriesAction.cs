using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh all TMDB movies linked to the series.
/// </summary>
public sealed class RefreshTmdbMoviesSeriesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Refresh TMDB Movies";

    public override string? Description => "Refresh all linked TMDB movie metadata.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var xref in Series.TmdbMovieCrossReferences)
            await scheduler.Enqueue<UpdateTmdbMovieJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceRefresh = false; // body.Force default
                j.DownloadImages = true; // body.DownloadImages default
                j.DownloadCrewAndCast = null; // body.DownloadCrewAndCast default
                j.DownloadCollections = null; // body.DownloadCollections default
            }, ct: token);
    }
}
