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
[TmdbApiRateLimited]
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class UpdateTmdbMovieJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbMovieID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool? DownloadCrewAndCast { get; set; }

    public virtual bool? DownloadCollections { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual string? MovieTitle { get; set; }

    public override void PostInit()
    {
        MovieTitle ??= _tmdbMovies.GetByTmdbMovieID(TmdbMovieID)?.EnglishTitle;
    }

    public override string TypeName => string.IsNullOrEmpty(MovieTitle)
        ? "Download TMDB Movie"
        : "Update TMDB Movie";

    public override string Title => string.IsNullOrEmpty(MovieTitle)
        ? "Downloading TMDB Movie"
        : "Updating TMDB Movie";

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
        _logger.LogInformation("Processing UpdateTmdbMovieJob: {TmdbMovieId}", TmdbMovieID);
        await _tmdbService.UpdateMovie(new()
        {
            MovieId = TmdbMovieID,
            ForceRefresh = ForceRefresh,
            DownloadImages = DownloadImages,
            DownloadCrewAndCast = DownloadCrewAndCast,
            DownloadCollections = DownloadCollections,
        }).ConfigureAwait(false);
    }

    private readonly TMDB_MovieRepository _tmdbMovies;
    public UpdateTmdbMovieJob(TmdbMetadataService tmdbService,
        TMDB_MovieRepository tmdbMovies
    )
    {
        _tmdbService = tmdbService;
        _tmdbMovies = tmdbMovies;

    }

    protected UpdateTmdbMovieJob() { }
}
