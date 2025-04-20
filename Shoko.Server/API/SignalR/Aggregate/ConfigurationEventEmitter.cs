using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class ConfigurationEventEmitter : BaseEventEmitter, IDisposable
{
    private IConfigurationService EventHandler { get; set; }

    public ConfigurationEventEmitter(IHubContext<AggregateHub> hub, IConfigurationService events) : base(hub)
    {
        EventHandler = events;
        EventHandler.Saved += OnSaved;
        EventHandler.RequiresRestart += OnRequiresRestart;
    }

    public void Dispose()
    {
        EventHandler.Saved -= OnSaved;
        EventHandler.RequiresRestart -= OnRequiresRestart;
    }

    private async void OnSaved(object? sender, ConfigurationSavedEventArgs eventArgs)
    {
        await SendAsync("saved", new ConfigurationSavedSignalRModel(eventArgs));
    }

    private async void OnRequiresRestart(object? sender, ConfigurationRequiresRestartEventArgs eventArgs)
    {
        await SendAsync("requiresRestart", new ConfigurationRequiresRestartSignalRModel() { RequiresRestart = eventArgs.RequiresRestart });
    }

    protected override object[] GetInitialMessages()
    {
        return [new ConfigurationRequiresRestartSignalRModel() { RequiresRestart = EventHandler.RestartPendingFor.Count > 0 }];
    }
}
