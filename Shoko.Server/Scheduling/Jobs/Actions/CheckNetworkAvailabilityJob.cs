using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
public class CheckNetworkAvailabilityJob(IConnectivityService connectivityService) : BaseJob
{
    public override string TypeName => "Check Network Availability";

    public override string Title => "Checking Network Availability";

    public override async Task Execute()
    {
        await connectivityService.CheckAvailability();
    }
}
