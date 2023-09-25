// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
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

    private readonly ConnectivityService _connectivityService;

    public ConnectivityMonitorJob(ISettingsProvider settingsProvider, IEnumerable<IConnectivityMonitor> connectivityMonitors, IConnectivityService connectivityService, ILogger<ConnectivityMonitorJob> logger)
    {
        _settingsProvider = settingsProvider;
        _connectivityMonitors = connectivityMonitors.ToArray();
        _connectivityService = connectivityService as ConnectivityService;
        _logger = logger;
    }

    protected ConnectivityMonitorJob() { }

    public async Task Execute(IJobExecutionContext context)
    {
        try 
        {
            var localNetwork = GetLANConnectivity();
            if (localNetwork != NetworkAvailability.LocalOnly)
            {
                _connectivityService.NetworkAvailability = localNetwork;
                return;
            }

            var wideNetwork = await GetWANConnectivity();
            _connectivityService.NetworkAvailability = wideNetwork;
        } catch (Exception ex) {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
    
    private NetworkAvailability GetLANConnectivity()
    {
        _logger.LogInformation("Checking LAN Connectivity…");
        // Get all active network interfaces
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .ToList();

        if (!networkInterfaces.Any())
            return NetworkAvailability.NoInterfaces;

        foreach (var netInterface in networkInterfaces)
        {
            var properties = netInterface.GetIPProperties();
            if (properties == null)
                continue;

            var defaultGateway = properties.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault();
            if (defaultGateway == null)
                continue;

            _logger.LogInformation("Found a local gateway to use.");
            return NetworkAvailability.LocalOnly;
        }

        _logger.LogInformation("No local gateway was found.");
        return NetworkAvailability.NoGateways;
    }

    private async Task<NetworkAvailability> GetWANConnectivity()
    {
        var currentlyDisabledMonitors = _settingsProvider.GetSettings().Connectivity.DisabledMonitorServices
            .ToHashSet();
        var monitors = _connectivityMonitors
            .Where(monitor => !currentlyDisabledMonitors.Contains(monitor.Service, StringComparer.InvariantCultureIgnoreCase))
            .ToList();
        if (monitors.Count == 0)
        {
            _logger.LogInformation("Skipped checking WAN Connectivity.");
            return NetworkAvailability.Internet;
        }

        _logger.LogInformation("Checking WAN Connectivity…");
        await Parallel.ForEachAsync(monitors, async (monitor, token) =>
        {
            await monitor.ExecuteCheckAsync(token);
        });

        var connectedCount = monitors.Count(a => a.HasConnected);
        _logger.LogInformation("Successfully connected to {Count}/{Total} internet service endpoints.", connectedCount,
            monitors.Count);

        return connectedCount > 0 ? (
            // We managed to connect to WAN, either partially or fully.
            connectedCount == monitors.Count ? NetworkAvailability.Internet : NetworkAvailability.PartialInternet
        ) : (
            // We didn't manage to connect to WAN, but we reached the gateway
            NetworkAvailability.LocalOnly
        );
    }
}
