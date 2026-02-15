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
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class UpdateTmdbShowJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    private readonly ISettingsProvider _settingsProvider;

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

    public override async Task Process()
    {
        _logger.LogInformation("Processing UpdateTmdbShowJob: {TmdbShowId}", TmdbShowID);
        var settings = _settingsProvider.GetSettings();
        await _tmdbService.UpdateShow(
            showId: TmdbShowID,
            forceRefresh: ForceRefresh,
            downloadImages: DownloadImages,
            downloadCrewAndCast: DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast,
            downloadAlternateOrdering: DownloadAlternateOrdering ?? settings.TMDB.AutoDownloadAlternateOrdering,
            downloadNetworks: DownloadNetworks ?? settings.TMDB.AutoDownloadNetworks,
            quickRefresh: false
        ).ConfigureAwait(false);
    }

    public UpdateTmdbShowJob(TmdbMetadataService tmdbService, ISettingsProvider settingsProvider)
    {
        _tmdbService = tmdbService;
        _settingsProvider = settingsProvider;
    }

    protected UpdateTmdbShowJob() { }
}
