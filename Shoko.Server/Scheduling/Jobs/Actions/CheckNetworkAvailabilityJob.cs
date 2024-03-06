using System;
using System.Threading.Tasks;
using Quartz;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
public class CheckNetworkAvailabilityJob : IJob
{
    private readonly IConnectivityService _connectivityService;

    public CheckNetworkAvailabilityJob(IConnectivityService connectivityService)
    {
        _connectivityService = connectivityService;
    }

    protected CheckNetworkAvailabilityJob() { }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _connectivityService.CheckAvailability();
        }
        catch (Exception ex)
        {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
