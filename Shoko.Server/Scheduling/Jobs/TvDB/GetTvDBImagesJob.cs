using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TvDB)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class GetTvDBImagesJob : BaseJob
{
    private readonly TvDBApiHelper _helper;
    public int TvDBSeriesID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string Name => "Get TvDB Images";

    public override QueueStateStruct Description => new()
    {
        message = "Getting images from The TvDB: {0}",
        queueState = QueueStateEnum.DownloadTvDBImages,
        extraParams = new[] { TvDBSeriesID.ToString() }
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
