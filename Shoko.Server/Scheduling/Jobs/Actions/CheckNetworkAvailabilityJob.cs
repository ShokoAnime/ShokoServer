// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup("System")]
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
