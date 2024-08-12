using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class GetTvDBImagesJob : BaseJob
{
    private readonly TvDBApiHelper _helper;
    private string _seriesName;
    public int TvDBSeriesID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get TvDB Images for Series";
    public override string Title => "Getting TvDB Images for Series";
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

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {ID}", nameof(GetTvDBImagesJob), TvDBSeriesID);

        await _helper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);
    }

    public GetTvDBImagesJob(TvDBApiHelper helper)
    {
        _helper = helper;
    }

    protected GetTvDBImagesJob() { }
}
