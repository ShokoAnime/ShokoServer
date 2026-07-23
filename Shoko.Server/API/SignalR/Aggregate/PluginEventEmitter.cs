using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
///   Emits SignalR events for plugin lifecycle changes (install, uninstall,
///   enable, disable, pin, unpin).
/// </summary>
public class PluginEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IPluginManager _pluginManager;

    public PluginEventEmitter(IHubContext<AggregateHub> hub, IPluginManager pluginManager) : base(hub)
    {
        _pluginManager = pluginManager;
        _pluginManager.PluginInstalled += OnPluginInstalled;
        _pluginManager.PluginUninstalled += OnPluginUninstalled;
    }

    public void Dispose()
    {
        _pluginManager.PluginInstalled -= OnPluginInstalled;
        _pluginManager.PluginUninstalled -= OnPluginUninstalled;
    }

    private async void OnPluginInstalled(object? sender, PluginInstallationEventArgs e)
        => await SendAsync("installed", new PluginEventSignalRModel(e));

    private async void OnPluginUninstalled(object? sender, PluginInstallationEventArgs e)
        => await SendAsync("uninstalled", new PluginEventSignalRModel(e));
}
