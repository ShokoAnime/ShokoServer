using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Services.Connectivity;

public class ConnectivityService : IConnectivityService
{
    private readonly ILogger<ConnectivityService> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IConnectivityMonitor[] _connectivityMonitors;

    private NetworkAvailability _networkAvailability = NetworkAvailability.NoInterfaces;

    private DateTime _lastChangedAt = DateTime.Now;

    /// <inheritdoc/>
    public event EventHandler<NetworkAvailabilityChangedEventArgs>? NetworkAvailabilityChanged;

    /// <inheritdoc/>
    public NetworkAvailability NetworkAvailability
    {
        get => _networkAvailability;
        private set
        {
            var hasChanged = _networkAvailability != value;
            if (hasChanged)
            {
                _lastChangedAt = DateTime.Now;
                _networkAvailability = value;
                Task.Run(() => NetworkAvailabilityChanged?.Invoke(null, new(value, _lastChangedAt))).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public DateTime LastChangedAt =>
        _lastChangedAt;

    public ConnectivityService(ILogger<ConnectivityService> logger, ISettingsProvider settingsProvider, IEnumerable<IConnectivityMonitor> connectivityMonitors)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _connectivityMonitors = connectivityMonitors.ToArray();
    }

    public async Task<NetworkAvailability> CheckAvailability()
    {
        try
        {
            var localNetwork = GetLANConnectivity();
            if (localNetwork is NetworkAvailability.NoInterfaces)
                return NetworkAvailability = localNetwork;

            var wideNetwork = await GetWANConnectivity();
            return NetworkAvailability = wideNetwork ?? localNetwork;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check network availability");
            return NetworkAvailability;
        }
    }

    private NetworkAvailability GetLANConnectivity()
    {
        _logger.LogInformation("Checking LAN Connectivity…");
        // Get all active network interfaces
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n is { OperationalStatus: OperationalStatus.Up, NetworkInterfaceType: not NetworkInterfaceType.Loopback })
            .ToList();

        if (!networkInterfaces.Any())
            return NetworkAvailability.NoInterfaces;

        foreach (var netInterface in networkInterfaces)
        {
            var properties = netInterface.GetIPProperties();
            var defaultGateway = properties.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault();
            if (defaultGateway == null)
                continue;

            _logger.LogInformation("Found a local gateway to use");
            return NetworkAvailability.LocalOnly;
        }

        _logger.LogInformation("No local gateway was found");
        return NetworkAvailability.NoGateways;
    }

    private async Task<NetworkAvailability?> GetWANConnectivity()
    {
        var currentlyDisabledMonitors = _settingsProvider.GetSettings().Connectivity.DisabledMonitorServices
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        var monitors = _connectivityMonitors
            .Where(monitor => !currentlyDisabledMonitors.Contains(monitor.Service))
            .ToList();
        if (monitors.Count == 0)
        {
            _logger.LogInformation("Skipped checking WAN Connectivity");
            return NetworkAvailability.Internet;
        }

        _logger.LogInformation("Checking WAN Connectivity…");
        await Parallel.ForEachAsync(monitors, async (monitor, token) =>
        {
            await monitor.ExecuteCheckAsync(token);
        });

        var connectedCount = monitors.Count(a => a.HasConnected);
        _logger.LogInformation("Successfully connected to {Count}/{Total} internet service endpoints", connectedCount,
            monitors.Count);

        // We managed to connect to WAN, either partially or fully.
        return connectedCount > 0 ? connectedCount == monitors.Count ? NetworkAvailability.Internet : NetworkAvailability.PartialInternet : null;
    }
}
