using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;

#pragma warning disable CS8618
#nullable enable
using Shoko.Server.Repositories.Cached.TMDB;
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class PurgeTmdbMovieJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbMovieID { get; set; }

    public virtual string? MovieTitle { get; set; }

    public override void PostInit()
    {
        MovieTitle ??= _tmdbMovies.GetByTmdbMovieID(TmdbMovieID)?.EnglishTitle;
    }

    public override string TypeName => "Purge TMDB Movie";

    public override string Title => "Purging TMDB Movie";

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
        _logger.LogInformation("Processing PurgeTmdbMovieJob: {TmdbMovieId}", TmdbMovieID);
        await _tmdbService.PurgeMovie(TmdbMovieID).ConfigureAwait(false);
    }

    private readonly TMDB_MovieRepository _tmdbMovies;
    public PurgeTmdbMovieJob(TmdbMetadataService tmdbService,
        TMDB_MovieRepository tmdbMovies
    )
    {
        _tmdbService = tmdbService;
        _tmdbMovies = tmdbMovies;

    }

    protected PurgeTmdbMovieJob() { }
}
