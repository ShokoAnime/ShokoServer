using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class GetTraktSeriesJob : BaseJob
{
    private readonly TraktTVHelper _helper;
    public string TraktID { get; set; }

    public override string Name => "Get Trakt Series";

    public override QueueStateStruct Description => new()
    {
        message = "Getting Trakt Series: {0}",
        queueState = QueueStateEnum.UpdateTraktData,
        extraParams = new[] { TraktID }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> ID: {ID}", nameof(GetTraktSeriesJob), TraktID);
        _helper.UpdateAllInfo(TraktID);
        return Task.CompletedTask;
    }

    public GetTraktSeriesJob(TraktTVHelper helper)
    {
        _helper = helper;
    }

    protected GetTraktSeriesJob() { }
}
