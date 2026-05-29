using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

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

    public override async Task Execute()
    {
        await _connectivityService.CheckAvailability();
    }
}
