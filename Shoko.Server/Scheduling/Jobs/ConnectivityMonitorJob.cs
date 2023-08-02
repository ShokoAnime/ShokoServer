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
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyMember("UptimeMonitor")]
[JobKeyGroup("System")]
[DisallowConcurrentExecution]
public class ConnectivityMonitorJob : IJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly IConnectivityMonitor[] _connectivityMonitors;
    private readonly ILogger<ConnectivityMonitorJob> _logger;

    public ConnectivityMonitorJob(ISettingsProvider settingsProvider, IEnumerable<IConnectivityMonitor> connectivityMonitors, ILogger<ConnectivityMonitorJob> logger)
    {
        _settingsProvider = settingsProvider;
        _connectivityMonitors = connectivityMonitors.ToArray();
        _logger = logger;
    }

    protected ConnectivityMonitorJob() { }

    public async Task Execute(IJobExecutionContext context)
    {
        try 
        {
            var currentlyDisabledMonitors = _settingsProvider.GetSettings().Connectivity.DisabledMonitorServices
                .ToHashSet();
            var monitors = _connectivityMonitors
                .Where(monitor => !currentlyDisabledMonitors.Contains(monitor.Service.ToLowerInvariant()))
                .ToList();

            if (monitors.Count == 0)
            {
                _logger.LogInformation("Skipped checking Network Connectivity");
                return;
            }

            // get data out of the MergedJobDataMap
            //var value = context.MergedJobDataMap.GetString("some-value");
            _logger.LogInformation("Checking Network Connectivity");
            await Parallel.ForEachAsync(monitors, async (monitor, token) =>
            {
                await monitor.ExecuteCheckAsync(token);
            });

            _logger.LogInformation("Successfully connected to {Count}/{Total} services", monitors.Count(a => a.HasConnected),
                monitors.Count);
            // ... do work
        } catch (Exception ex) {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
