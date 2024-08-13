using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Actions)]
public class RefreshAnimeStatsJob : BaseJob
{
    private readonly AniDB_AnimeRepository _animeRepo;
    private readonly AnimeSeriesRepository _seriesRepo;
    private readonly AnimeSeriesService _seriesService;
    private readonly AnimeGroupService _groupService;

    public int AnimeID { get; set; }
    private string _anime;

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
        _anime = _animeRepo.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(RefreshAnimeStatsJob), _anime);
        var anime = _animeRepo.GetByAnimeID(AnimeID);
        if (anime == null)
        {
            _logger.LogWarning("AniDB_Anime not found: {AnimeID}", AnimeID);
            return Task.CompletedTask;
        }
        _animeRepo.Save(anime);
        var series = _seriesRepo.GetByAnimeID(AnimeID);
        // Updating stats saves everything and updates groups
        _seriesService.UpdateStats(series, true, true);
        _groupService.UpdateStatsFromTopLevel(series?.AnimeGroup?.TopLevelAnimeGroup, true, true);
        return Task.CompletedTask;
    }

    public RefreshAnimeStatsJob(AnimeSeriesService seriesService, AniDB_AnimeRepository animeRepo, AnimeSeriesRepository seriesRepo, AnimeGroupService groupService)
    {
        _seriesService = seriesService;
        _animeRepo = animeRepo;
        _seriesRepo = seriesRepo;
        _groupService = groupService;
    }

    protected RefreshAnimeStatsJob() { }
}
