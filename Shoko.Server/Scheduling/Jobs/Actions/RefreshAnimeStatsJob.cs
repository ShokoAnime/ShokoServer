using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Actions)]
public class RefreshAnimeStatsJob : BaseJob
{
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
        _anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(RefreshAnimeStatsJob), _anime);
        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
        return Task.CompletedTask;
    }
}
