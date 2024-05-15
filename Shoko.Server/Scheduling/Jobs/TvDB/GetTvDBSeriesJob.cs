using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Server;
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
public class GetTvDBSeriesJob : BaseJob<TvDB_Series>
{
    private readonly TvDBApiHelper _helper;
    private string _seriesName;
    public int TvDBSeriesID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get TvDB Series Data";

    public override string Title => "Getting TvDB Series Data";
    public override void PostInit()
    {
        _seriesName = RepoFactory.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName;
    }

    public override Dictionary<string, object> Details => new()
        {
            {
                "Series", _seriesName ?? TvDBSeriesID.ToString()
            }
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
