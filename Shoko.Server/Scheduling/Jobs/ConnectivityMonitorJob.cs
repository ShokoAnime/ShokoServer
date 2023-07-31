// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Services.Connectivity;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup("System")]
[DisallowConcurrentExecution]
public class ConnectivityMonitorJob : IJob
{
    private readonly IConnectivityMonitor[] _connectivityMonitors;
    private readonly ILogger<ConnectivityMonitorJob> _logger;

    public ConnectivityMonitorJob(IEnumerable<IConnectivityMonitor> connectivityMonitors, ILogger<ConnectivityMonitorJob> logger)
    {
        _connectivityMonitors = connectivityMonitors.ToArray();
        _logger = logger;
    }

    protected ConnectivityMonitorJob() { }

    public async Task Execute(IJobExecutionContext context)
    {
        try 
        {
            // get data out of the MergedJobDataMap
            //var value = context.MergedJobDataMap.GetString("some-value");
            _logger.LogInformation("Checking Network Connectivity");
            await Parallel.ForEachAsync(_connectivityMonitors, async (monitor, token) =>
            {
                await monitor.ExecuteCheckAsync(token);
            });

            _logger.LogInformation("Successfully connected to {Count}/{Total} services", _connectivityMonitors.Count(a => a.HasConnected),
                _connectivityMonitors.Length);
            // ... do work
        } catch (Exception ex) {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
