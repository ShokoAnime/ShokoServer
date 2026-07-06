using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Actions)]
public class RefreshAnimeStatsJob(
    AnimeSeriesService seriesService,
    AniDB_AnimeRepository animeRepo,
    AnimeSeriesRepository seriesRepo,
    AnimeGroupService groupService
) : BaseJob
{
    public int AnimeID { get; set; }

    private string? _animeName;

    public override string TypeName => "Refresh Anime Stats";
    public override string Title => "Refreshing Anime Stats";
    public override Dictionary<string, object> Details => new()
    {
        {
            "AnimeID", AnimeID
        }
    };

    public override void PostInit()
    {
        _animeName = animeRepo.GetByAnimeID(AnimeID)?.Title ?? AnimeID.ToString();
    }

    public override Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(RefreshAnimeStatsJob), _animeName);
        var anime = animeRepo.GetByAnimeID(AnimeID);
        if (anime == null)
        {
            _logger.LogWarning("AniDB_Anime not found: {AnimeID}", AnimeID);
            return Task.CompletedTask;
        }
        var series = seriesRepo.GetByAnimeID(AnimeID);
        if (series is not null)
        {
            series.ResetAnimeTitles();
            series.ResetPreferredTitle();
            series.ResetPreferredOverview();
        }

        // Updating stats saves everything and updates groups
        seriesService.UpdateStats(series, true, true);
        groupService.UpdateStatsFromTopLevel(series?.AnimeGroup?.TopLevelAnimeGroup, true, true);
        return Task.CompletedTask;
    }
}
