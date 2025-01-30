using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class NetworkEmitter : BaseEmitter, IDisposable
{
    private IConnectivityService EventHandler { get; set; }

    public NetworkEmitter(IHubContext<AggregateHub> hub, IConnectivityService events) : base(hub)
    {
        EventHandler = events;
        EventHandler.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public void Dispose()
    {
        EventHandler.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    private async void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityChangedEventArgs eventArgs)
    {
        await SendAsync("NetworkAvailabilityChanged", new NetworkAvailabilitySignalRModel(eventArgs));
    }

    public override object GetInitialMessage()
    {
        return new NetworkAvailabilitySignalRModel(EventHandler.NetworkAvailability, EventHandler.LastChangedAt);
    }
}
