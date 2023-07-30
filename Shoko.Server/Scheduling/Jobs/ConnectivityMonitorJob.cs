// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Services.ConnectivityMon;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup("System")]
[DisallowConcurrentExecution]
public class ConnectivityMonitorJob : IJob
{
    private readonly IEnumerable<IConnectivityMonitor> _connectivityMonitors;

    public ConnectivityMonitorJob(IEnumerable<IConnectivityMonitor> connectivityMonitors)
    {
        _connectivityMonitors = connectivityMonitors;
    }

    protected ConnectivityMonitorJob() { }

    public async Task Execute(IJobExecutionContext context)
    {
        try 
        {
            // get data out of the MergedJobDataMap
            //var value = context.MergedJobDataMap.GetString("some-value");
            // TODO: Logging
            await Parallel.ForEachAsync(_connectivityMonitors, async (monitor, token) =>
            {
                await monitor.ExecuteCheckAsync();
            });
            // ... do work
        } catch (Exception ex) {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
