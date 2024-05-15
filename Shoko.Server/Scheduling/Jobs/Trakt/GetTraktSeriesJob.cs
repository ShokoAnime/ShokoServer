using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
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

    public override string TypeName => "Get Trakt Series Data";
    public override string Title => "Getting Trakt Series Data";
    public override Dictionary<string, object> Details => new() { { "TraktID", TraktID } };

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
