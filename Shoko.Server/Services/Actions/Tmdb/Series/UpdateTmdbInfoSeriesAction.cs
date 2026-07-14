using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Update all TMDB shows linked to the series.
/// </summary>
public sealed class UpdateTmdbInfoSeriesAction(
    IJobFactory jobFactory
) : IExecutableSeriesSystemAction
{
    public string Name => "Update TMDB Info";
    public string? Description => "Gets the latest series information from TMDB.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        foreach (var xref in series.TmdbShowCrossReferences)
            await jobFactory.Execute<UpdateTmdbShowJob>(j =>
            {
                j.TmdbShowID = xref.TmdbShowID;
                j.ForceRefresh = false;
                j.DownloadImages = false;
            });
        foreach (var xref in series.TmdbMovieCrossReferences)
            await jobFactory.Execute<UpdateTmdbMovieJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceRefresh = false;
                j.DownloadImages = false;
            });
    }
}
