using System.Threading.Tasks;
using Quartz;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
public class CheckNetworkAvailabilityJob : BaseJob
{
    private readonly IConnectivityService _connectivityService;
    public override string TypeName => "Check Network Availability";
    public override string Title => "Checking Network Availability";

    public CheckNetworkAvailabilityJob(IConnectivityService connectivityService)
    {
        _connectivityService = connectivityService;
    }

    protected CheckNetworkAvailabilityJob() { }

    public override async Task Process()
    {
        await _connectivityService.CheckAvailability();
    }
}
