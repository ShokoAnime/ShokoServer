using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all TMDB shows linked to the series.
/// </summary>
public sealed class UpdateTmdbInfoSeriesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Update TMDB Info";

    public string? Description => "Gets the latest series information from TMDB.";

    public ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var xref in Series.TmdbShowCrossReferences)
            await scheduler.Enqueue<UpdateTmdbShowJob>(j =>
            {
                j.TmdbShowID = xref.TmdbShowID;
                j.ForceRefresh = false; // body.Force default
                j.DownloadImages = true; // body.DownloadImages default
                j.DownloadCrewAndCast = null; // body.DownloadCrewAndCast default
                j.DownloadAlternateOrdering = null; // body.DownloadAlternateOrdering default
                j.DownloadNetworks = null; // body.DownloadNetworks default
            }, ct: token);
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
