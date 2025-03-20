using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Aggregate;

public class ConfigurationEmitter : BaseEmitter, IDisposable
{
    private IConfigurationService EventHandler { get; set; }

    public ConfigurationEmitter(IHubContext<AggregateHub> hub, IConfigurationService events) : base(hub)
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
        await SendAsync("Saved", new ConfigurationSavedSignalRModel(eventArgs));
    }

    private async void OnRequiresRestart(object? sender, ConfigurationRequiresRestartEventArgs eventArgs)
    {
        await SendAsync("RequiresRestart", new ConfigurationRequiresRestartSignalRModel() { RequiresRestart = eventArgs.RequiresRestart });
    }

    public override object GetInitialMessage()
    {
        return new ConfigurationRequiresRestartSignalRModel() { RequiresRestart = EventHandler.RestartPendingFor.Count > 0 };
    }
}
