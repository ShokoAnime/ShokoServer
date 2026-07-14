using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Force a complete redownload of TMDB images for the series.
/// </summary>
public sealed class UpdateTmdbImagesForceSeriesAction(IJobFactory jobFactory) : IExecutableSeriesSystemAction
{
    public string Name => "Update TMDB Images - Force";
    public string? Description => "Forces a complete redownload of images from TMDB.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        foreach (var xref in series.TmdbShowCrossReferences)
            await jobFactory.Execute<DownloadTmdbShowImagesJob>(j =>
            {
                j.TmdbShowID = xref.TmdbShowID;
                j.ForceDownload = true;
            });
        foreach (var xref in series.TmdbMovieCrossReferences)
            await jobFactory.Execute<DownloadTmdbMovieImagesJob>(j =>
            {
                j.TmdbMovieID = xref.TmdbMovieID;
                j.ForceDownload = true;
            });
    }
}
