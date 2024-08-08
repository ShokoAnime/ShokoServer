using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(4, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class PurgeTmdbMovieJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbMovieID { get; set; }

    public virtual bool RemoveImageFiles { get; set; } = true;

    public virtual string? MovieTitle { get; set; }

    public override void PostInit()
    {
        MovieTitle ??= RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID)?.EnglishTitle;
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

    public override async Task Process()
    {
        _logger.LogInformation("Processing CommandRequest_TMDB_Movie_Purge: {TmdbMovieId}", TmdbMovieID);
        await Task.Run(() => _tmdbService.PurgeMovie(TmdbMovieID, RemoveImageFiles)).ConfigureAwait(false);
    }

    public PurgeTmdbMovieJob(TmdbMetadataService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    protected PurgeTmdbMovieJob() { }
}
