using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;

#pragma warning disable CS8618
using Shoko.Server.Repositories.Cached.TMDB;
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[TmdbApiRateLimited]
[LongRunning]
[LimitConcurrency(1, 16)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class DownloadTmdbShowImagesJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbShowID { get; set; }

    public virtual bool ForceDownload { get; set; } = true;

    public virtual string? ShowTitle { get; set; }

    public override void PostInit()
    {
        ShowTitle ??= _tmdbShows.GetByTmdbShowID(TmdbShowID)?.EnglishTitle;
    }

    public override string TypeName => "Download Images for TMDB Show";

    public override string Title => "Downloading Images for TMDB Show";

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
        _logger.LogInformation("Processing DownloadTmdbShowImagesJob: {TmdbShowId}", TmdbShowID);
        await Task.Run(() => _tmdbService.DownloadAllShowImages(TmdbShowID, ForceDownload)).ConfigureAwait(false);
    }

    private readonly TMDB_ShowRepository _tmdbShows;
    public DownloadTmdbShowImagesJob(TmdbMetadataService tmdbService,
        TMDB_ShowRepository tmdbShows
    )
    {
        _tmdbService = tmdbService;
        _tmdbShows = tmdbShows;

    }

    protected DownloadTmdbShowImagesJob() { }
}
