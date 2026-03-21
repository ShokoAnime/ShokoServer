using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Events;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Server.Settings;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

#nullable enable
namespace Shoko.Server.Services.Connectivity;

public class ConnectivityService : IConnectivityService
{
    private static readonly List<ConnectivityMonitorDefinition> _defaultDefinitions =
    [
        new() { Name = "CloudFlare", Type = ConnectivityCheckType.Head, Address = "https://1.1.1.1/" },
        new() { Name = "Mozilla", Type = ConnectivityCheckType.Head, Address = "https://detectportal.firefox.com/success.txt" },
        new() { Name = "WeChat", Type = ConnectivityCheckType.Get, Address = "https://www.wechat.com/" },
    ];

    private readonly ILogger<ConnectivityService> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly List<ConnectivityMonitorDefinition> _monitorDefinitions;

    private readonly HttpClient _httpClient;

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

    public ConnectivityService(ILogger<ConnectivityService> logger, ISettingsProvider settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = delegate { return true; }
            }
        })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var settings = _settingsProvider.GetSettings().Connectivity;
        _monitorDefinitions = settings.MonitorDefinitions is { Count: > 0 }
            ? [.. settings.MonitorDefinitions]
            : [.. _defaultDefinitions];
    }

    /// <inheritdoc/>
    public IReadOnlyList<IConnectivityMonitor> GetMonitorDefinitions() => _monitorDefinitions;

    /// <inheritdoc/>
    public IConnectivityMonitor AddMonitorDefinition(string name, ConnectivityCheckType type, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (_monitorDefinitions.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A monitor definition with the name '{name}' already exists.", nameof(name));

        if (!Uri.TryCreate(address, UriKind.Absolute, out _))
            throw new ArgumentException($"The address '{address}' is not a valid absolute URI.", nameof(address));

        var definition = new ConnectivityMonitorDefinition
        {
            Name = name,
            Type = type,
            Address = address,
        };
        _monitorDefinitions.Add(definition);
        SaveDefinitions();

        return definition;
    }

    /// <inheritdoc/>
    public bool RemoveMonitorDefinition(string name)
    {
        var index = _monitorDefinitions.FindIndex(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        _monitorDefinitions.RemoveAt(index);
        SaveDefinitions();

        return true;
    }

    /// <inheritdoc/>
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

    private void SaveDefinitions()
    {
        var settings = _settingsProvider.GetSettings();
        settings.Connectivity.MonitorDefinitions = [.. _monitorDefinitions];
        _settingsProvider.SaveSettings(settings);
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
        var definitions = _monitorDefinitions.ToList();
        if (definitions.Count == 0)
        {
            _logger.LogInformation("Skipped checking WAN Connectivity");
            return NetworkAvailability.Internet;
        }

        _logger.LogInformation("Checking WAN Connectivity…");

        var results = new bool[definitions.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, definitions.Count),
            async (i, token) =>
            {
                results[i] = await CheckEndpointAsync(definitions[i], token);
            });

        var connectedCount = results.Count(r => r);
        _logger.LogInformation("Successfully connected to {Count}/{Total} internet service endpoints", connectedCount, definitions.Count);

        return connectedCount > 0
            ? connectedCount == definitions.Count
                ? NetworkAvailability.Internet
                : NetworkAvailability.PartialInternet
            : null;
    }

    private async Task<bool> CheckEndpointAsync(IConnectivityMonitor monitor, CancellationToken token)
    {
        var method = monitor.Type switch
        {
            ConnectivityCheckType.Get => HttpMethod.Get,
            ConnectivityCheckType.Head => HttpMethod.Head,
            _ => HttpMethod.Head,
        };

        _logger.LogTrace("Trying to connect to {Service} ({Address})", monitor.Name, monitor.Address);
        using var request = new HttpRequestMessage(method, new Uri(monitor.Address));
        var sw = Stopwatch.StartNew();
        try
        {
            using var result = await _httpClient.SendAsync(request, token);
            sw.Stop();
            if (result.IsSuccessStatusCode)
            {
                _logger.LogTrace("Successfully connected to {Service} in {Time}ms", monitor.Name, sw.ElapsedMilliseconds);
                return true;
            }

            _logger.LogTrace("Received a failed status code from {Service} in {Time}ms: {Code}", monitor.Name, sw.ElapsedMilliseconds, (int)result.StatusCode);
            return false;
        }
        catch
        {
            sw.Stop();
            _logger.LogTrace("Failed to connect to {Service} after {Time}ms", monitor.Name, sw.ElapsedMilliseconds);
            return false;
        }
    }
}
