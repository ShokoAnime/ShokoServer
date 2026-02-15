using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class NetworkEventEmitter : BaseEventEmitter, IDisposable
{
    private IConnectivityService EventHandler { get; set; }

    public NetworkEventEmitter(IHubContext<AggregateHub> hub, IConnectivityService events) : base(hub)
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
        await SendAsync("availabilityChanged", new NetworkAvailabilitySignalRModel(eventArgs));
    }

    protected override object[] GetInitialMessages()
    {
        return [new NetworkAvailabilitySignalRModel(EventHandler.NetworkAvailability, EventHandler.LastChangedAt)];
    }
}
