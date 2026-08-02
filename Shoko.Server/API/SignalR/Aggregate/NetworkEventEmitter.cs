using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Events;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class NetworkEventEmitter : BaseEventEmitter, IDisposable
{
    private IConnectivityService EventHandler { get; set; }

    private readonly ILogger<NetworkEventEmitter> _logger;

    public NetworkEventEmitter(IHubContext<AggregateHub> hub, IConnectivityService events, ILogger<NetworkEventEmitter> logger) : base(hub)
    {
        EventHandler = events;
        _logger = logger;
        EventHandler.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public void Dispose()
    {
        EventHandler.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    private async void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityChangedEventArgs eventArgs)
    {
        try
        {
            await SendAsync("availabilityChanged", new NetworkAvailabilitySignalRModel(eventArgs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'availabilityChanged' event.");
        }
    }

    protected override object[] GetInitialMessages()
    {
        return [new NetworkAvailabilitySignalRModel(EventHandler.NetworkAvailability, EventHandler.LastChangedAt)];
    }
}
