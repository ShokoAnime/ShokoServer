using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TvDB)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class LinkTvDBSeriesJob : BaseJob
{
    private readonly TvDBApiHelper _helper;
    private readonly JobFactory _jobFactory; 
    private string _animeName;
    private string _seriesName;
    public int AnimeID { get; set; }
    public int TvDBID { get; set; }
    public bool AdditiveLink { get; set; }

    public override string TypeName => "Link TvDB Series";
    public override string Title => "Linking TvDB Series";
    public override void PostInit()
    {
        _animeName = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle;
        _seriesName = RepoFactory.TvDB_Series.GetByTvDBID(TvDBID)?.SeriesName;
    }

    public override Dictionary<string, object> Details => new()
        {
            {
                "Anime", _animeName ?? AnimeID.ToString()
            },
            {
                "TvDB Series", _seriesName ?? TvDBID.ToString()
            }
        };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} -> TvDB: {TvDB} | AniDB: {AniDB} | Additive: {Additive}", nameof(LinkTvDBSeriesJob), TvDBID, AnimeID,
            AdditiveLink);

        await _helper.LinkAniDBTvDB(AnimeID, TvDBID, AdditiveLink);
        await _jobFactory.CreateJob<RefreshAnimeStatsJob>(x => x.AnimeID = AnimeID).Process();
    }

    public LinkTvDBSeriesJob(TvDBApiHelper helper, JobFactory jobFactory)
    {
        _helper = helper;
        _jobFactory = jobFactory;
    }

    protected LinkTvDBSeriesJob() { }
}
