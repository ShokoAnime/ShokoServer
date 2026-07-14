using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Refresh all TMDB movies linked to the series.
/// </summary>
public sealed class RefreshTmdbMoviesSeriesAction(IJobFactory jobFactory) : IExecutableSeriesSystemAction
{
    public string Name => "Refresh TMDB Movies";
    public string? Description => "Refresh all linked TMDB movie metadata.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        foreach (var xref in series.TmdbMovieCrossReferences)
            await jobFactory.Execute<UpdateTmdbMovieJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceRefresh = false;
                j.DownloadImages = false;
            });
    }
}
