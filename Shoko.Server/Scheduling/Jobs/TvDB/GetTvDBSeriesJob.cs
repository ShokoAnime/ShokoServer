using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TvDB)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class GetTvDBSeriesJob : BaseJob<TvDB_Series>
{
    private readonly TvDBApiHelper _helper;
    public int TvDBSeriesID { get; set; }
    public bool ForceRefresh { get; set; }
    public string SeriesTitle { get; set; }

    public override string Name => "Get TvDB Series";

    public override void PostInit()
    {
        SeriesTitle = RepoFactory.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ?? string.Intern("Name not Available");
    }

    public override QueueStateStruct Description => new()
    {
        message = "Updating TvDB Series: {0}",
        queueState = QueueStateEnum.GettingTvDBSeries,
        extraParams = new[] { $"{SeriesTitle} ({TvDBSeriesID})" }
    };

    public override async Task<TvDB_Series> Process()
    {
        _logger.LogInformation("Processing {Job}: {ID}", nameof(GetTvDBSeriesJob), TvDBSeriesID);

        return await _helper.UpdateSeriesInfoAndImages(TvDBSeriesID, ForceRefresh, true);
    }

    public GetTvDBSeriesJob(TvDBApiHelper helper)
    {
        _helper = helper;
    }

    protected GetTvDBSeriesJob() { }
}
