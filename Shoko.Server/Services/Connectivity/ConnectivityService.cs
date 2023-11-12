using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.Interfaces;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Services.Connectivity;

public class ConnectivityService : IConnectivityService
{
    private readonly ILogger<ConnectivityService> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IConnectivityMonitor[] _connectivityMonitors;

    private readonly IUDPConnectionHandler _anidbUdpHandler;

    private readonly IHttpConnectionHandler _anidbHttpHandler;

    private readonly CommandProcessor _generalQueue;

    private readonly CommandProcessor _imagesQueue;

    private NetworkAvailability _networkAvailability { get; set; } = NetworkAvailability.NoInterfaces;

    /// <inheritdoc/>
    public event EventHandler<NetworkAvailabilityChangedEventArgs> NetworkAvailabilityChanged;

    /// <inheritdoc/>
    public NetworkAvailability NetworkAvailability
    {
        get => _networkAvailability;
        private set
        {
            var hasChanged = _networkAvailability != value;
            _networkAvailability = value;
            if (hasChanged)
                Task.Run(() => NetworkAvailabilityChanged?.Invoke(null, new(value))).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public bool IsAniDBUdpReachable =>
        _anidbUdpHandler.IsNetworkAvailable;

    /// <inheritdoc/>
    public bool IsAniDBHttpBanned =>
        _anidbHttpHandler.IsBanned;

    /// <inheritdoc/>
    public bool IsAniDBUdpBanned =>
        _anidbUdpHandler.IsBanned;

    public ConnectivityService(ILogger<ConnectivityService> logger, ISettingsProvider settingsProvider, IEnumerable<IConnectivityMonitor> connectivityMonitors, IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler, CommandProcessorGeneral generalQueue, CommandProcessorImages imagesQueue)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _connectivityMonitors = connectivityMonitors.ToArray();
        _anidbUdpHandler = udpHandler;
        _anidbHttpHandler = httpHandler;
        _generalQueue = generalQueue;
        _imagesQueue = imagesQueue;
        NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    ~ConnectivityService()
    {
        NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    public async Task<NetworkAvailability> CheckAvailability()
    {
        try
        {
            var localNetwork = GetLANConnectivity();
            if (localNetwork != NetworkAvailability.LocalOnly)
            {
                return NetworkAvailability = localNetwork;
            }

            var wideNetwork = await GetWANConnectivity();
            return NetworkAvailability = wideNetwork;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check network availability.");
            return NetworkAvailability;
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

    // Notify the queues that they can start again.
    private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityChangedEventArgs eventArgs)
    {
        if (!eventArgs.NetworkAvailability.HasInternet())
            return;

        if (!_generalQueue.Paused && _generalQueue.QueueCount > 0)
            _generalQueue.NotifyOfNewCommand();

        if (!_imagesQueue.Paused && _imagesQueue.QueueCount > 0)
            _imagesQueue.NotifyOfNewCommand();
    }
}
