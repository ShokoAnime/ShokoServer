using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TMDB)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class UpdateTmdbMovieJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    private readonly ISettingsProvider _settingsProvider;

    public virtual int TmdbMovieID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool? DownloadCrewAndCast { get; set; }

    public virtual bool? DownloadCollections { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual string? MovieTitle { get; set; }

    public override void PostInit()
    {
        MovieTitle ??= RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID)?.EnglishTitle;
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

    public override async Task Process()
    {
        _logger.LogInformation("Processing CommandRequest_TMDB_Movie_Update: {TmdbMovieId}", TmdbMovieID);
        var settings = _settingsProvider.GetSettings();
        await Task.Run(() => _tmdbService.UpdateMovie(TmdbMovieID, ForceRefresh, DownloadImages, DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast, DownloadCollections ?? settings.TMDB.AutoDownloadCollections)).ConfigureAwait(false);
    }

    public UpdateTmdbMovieJob(TmdbMetadataService tmdbService, ISettingsProvider settingsProvider)
    {
        _tmdbService = tmdbService;
        _settingsProvider = settingsProvider;
    }

    protected UpdateTmdbMovieJob() { }
}
