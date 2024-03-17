using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR;

public class InitEmitter : IDisposable
{
    public const string Group = "Init";
    private IHubContext<InitHub> Hub { get; set; }
    private readonly Dictionary<string, object> _lastServerState = new();

    public InitEmitter(IHubContext<InitHub> hub)
    {
        Hub = hub;
        ServerState.Instance.PropertyChanged += ServerStatePropertyChanged;
    }

    public void Dispose()
    {
        ServerState.Instance.PropertyChanged -= ServerStatePropertyChanged;
    }

    public async Task OnConnectedAsync(IClientProxy caller)
    {
        await caller.SendAsync("OnConnected", ServerState.Instance);
    }

    private async void ServerStatePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        if (e.PropertyName != "DatabaseBlocked" && !e.PropertyName.StartsWith("Server")) return;

        var value = e.GetPropertyValue(sender);
        if (_lastServerState.ContainsKey(e.PropertyName) && _lastServerState.TryGetValue(e.PropertyName, out var previousState) &&
            Equals(previousState, value)) return;

        _lastServerState[e.PropertyName] = value;
        await Hub.Clients.Group(Group).SendCoreAsync("ServerStateChanged", new[]
        {
            e.PropertyName, value
        });
    }
}
