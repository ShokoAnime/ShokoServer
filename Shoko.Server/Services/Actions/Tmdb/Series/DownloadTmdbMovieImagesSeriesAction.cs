using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Download images for all TMDB movies linked to the series.
/// </summary>
internal sealed class DownloadTmdbMovieImagesSeriesAction(IJobFactory jobFactory) : IExecutableSeriesAction
{
    public string Name => "Download TMDB Movie Images";
    public string? Description => "Download any missing images for linked TMDB movies.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        foreach (var xref in series.TmdbMovieCrossReferences)
            await jobFactory.Execute<DownloadTmdbMovieImagesJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceDownload = false;
            });
    }
}
