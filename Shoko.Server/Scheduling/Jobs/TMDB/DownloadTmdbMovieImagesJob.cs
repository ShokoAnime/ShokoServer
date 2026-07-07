using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Scheduling.Acquisition.Attributes;

#pragma warning disable CS8618
using Shoko.Server.Repositories.Cached.TMDB;
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[TmdbApiRateLimited]
[LimitConcurrency(1, 16)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class DownloadTmdbMovieImagesJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbMovieID { get; set; }

    public virtual bool ForceDownload { get; set; } = true;

    public virtual string? MovieTitle { get; set; }

    public override void PostInit()
    {
        MovieTitle ??= _tmdbMovies.GetByTmdbMovieID(TmdbMovieID)?.EnglishTitle;
    }

    public override string TypeName => "Download Images for TMDB Movie";

    public override string Title => "Downloading Images for TMDB Movie";

    public override Dictionary<string, object> Details => string.IsNullOrEmpty(MovieTitle)
        ? new()
        {
            {"MovieID", TmdbMovieID},
        }
        : new()
        {
            {"Movie", MovieTitle},
            {"MovieID", TmdbMovieID},
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing DownloadTmdbMovieImagesJob: {TmdbMovieId}", TmdbMovieID);
        await Task.Run(() => _tmdbService.DownloadAllMovieImages(TmdbMovieID, ForceDownload)).ConfigureAwait(false);
    }

    private readonly TMDB_MovieRepository _tmdbMovies;
    public DownloadTmdbMovieImagesJob(TmdbMetadataService tmdbService,
        TMDB_MovieRepository tmdbMovies
    )
    {
        _tmdbService = tmdbService;
        _tmdbMovies = tmdbMovies;

    }

    protected DownloadTmdbMovieImagesJob() { }
}
