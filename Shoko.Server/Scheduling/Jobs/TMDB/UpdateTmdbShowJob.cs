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
[LongRunning]
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class UpdateTmdbShowJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbShowID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool? DownloadCrewAndCast { get; set; }

    public virtual bool? DownloadAlternateOrdering { get; set; }

    public virtual bool? DownloadNetworks { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual bool QuickRefresh { get; set; }

    public virtual string? ShowTitle { get; set; }

    public override void PostInit()
    {
        ShowTitle ??= _tmdbShows.GetByTmdbShowID(TmdbShowID)?.EnglishTitle;
    }

    public override string TypeName => string.IsNullOrEmpty(ShowTitle)
        ? "Download TMDB Show"
        : "Update TMDB Show";

    public override string Title => string.IsNullOrEmpty(ShowTitle)
        ? "Downloading TMDB Show"
        : "Updating TMDB Show";

    public override Dictionary<string, object> Details => string.IsNullOrEmpty(ShowTitle)
        ? new()
        {
            {"ShowID", TmdbShowID},
        }
        : new()
        {
            {"Show", ShowTitle},
            {"ShowID", TmdbShowID},
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing UpdateTmdbShowJob: {TmdbShowId}", TmdbShowID);
        await _tmdbService.UpdateShow(new()
        {
            ShowId = TmdbShowID,
            ForceRefresh = ForceRefresh,
            QuickRefresh = QuickRefresh,
            DownloadImages = DownloadImages,
            DownloadCrewAndCast = DownloadCrewAndCast,
            DownloadAlternateOrdering = DownloadAlternateOrdering,
            DownloadNetworks = DownloadNetworks,
        }).ConfigureAwait(false);
    }

    private readonly TMDB_ShowRepository _tmdbShows;
    public UpdateTmdbShowJob(TmdbMetadataService tmdbService,
        TMDB_ShowRepository tmdbShows
    )
    {
        _tmdbService = tmdbService;
        _tmdbShows = tmdbShows;

    }

    protected UpdateTmdbShowJob() { }
}
