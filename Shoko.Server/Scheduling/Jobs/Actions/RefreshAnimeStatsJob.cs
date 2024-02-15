using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Actions)]
public class RefreshAnimeStatsJob : BaseJob
{
    public int AnimeID { get; set; }
    private string _anime;

    public override string Name => "Refresh Anime Stats";
    public override QueueStateStruct Description => new()
    {
        message = "Refreshing anime stats: {0}",
        queueState = QueueStateEnum.Refresh,
        extraParams = new[] { _anime }
    };

    public override void PostInit()
    {
        _anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(RefreshAnimeStatsJob), _anime);
        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
        return Task.CompletedTask;
    }
}
