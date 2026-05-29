using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
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

    public virtual string? ShowTitle { get; set; }

    public override void PostInit()
    {
        ShowTitle ??= RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID)?.EnglishTitle;
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
            DownloadImages = DownloadImages,
            DownloadCrewAndCast = DownloadCrewAndCast,
            DownloadAlternateOrdering = DownloadAlternateOrdering,
            DownloadNetworks = DownloadNetworks,
        }).ConfigureAwait(false);
    }

    public UpdateTmdbShowJob(TmdbMetadataService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    protected UpdateTmdbShowJob() { }
}
