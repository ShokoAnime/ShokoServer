using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TvDB)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class LinkTvDBSeriesJob : BaseJob
{
    private readonly TvDBApiHelper _helper;
    public int AnimeID { get; set; }
    public int TvDBID { get; set; }
    public bool AdditiveLink { get; set; }

    public override string TypeName => "Link TvDB Series";
    public override string Title => "Linking TvDB Series";
    public override Dictionary<string, object> Details => new()
    {
        { "Anime", RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString() },
        { "TvDB Series", RepoFactory.TvDB_Series.GetByTvDBID(TvDBID)?.SeriesName ?? TvDBID.ToString() }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} -> TvDB: {TvDB} | AniDB: {AniDB} | Additive: {Additive}", nameof(LinkTvDBSeriesJob), TvDBID, AnimeID,
            AdditiveLink);

        await _helper.LinkAniDBTvDB(AnimeID, TvDBID, AdditiveLink);
        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
    }

    public LinkTvDBSeriesJob(TvDBApiHelper helper)
    {
        _helper = helper;
    }

    protected LinkTvDBSeriesJob() { }
}
