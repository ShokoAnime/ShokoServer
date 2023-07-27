// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Services.ConnectivityMon;

namespace Shoko.Server.Scheduling.Jobs;

public class ConnectivityMonitorJob : IJob
{
    public static readonly JobKey Key = new("UptimeMonitor", "System");

    private readonly IEnumerable<IConnectivityMonitor> _connectivityMonitors;

    public ConnectivityMonitorJob(IEnumerable<IConnectivityMonitor> connectivityMonitors)
    {
        _connectivityMonitors = connectivityMonitors;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        try 
        {
            // get data out of the MergedJobDataMap
            //var value = context.MergedJobDataMap.GetString("some-vaule");
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
