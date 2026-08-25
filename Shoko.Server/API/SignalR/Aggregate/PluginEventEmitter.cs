using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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

    private readonly ILogger<PluginEventEmitter> _logger;

    public PluginEventEmitter(IHubContext<AggregateHub> hub, IPluginManager pluginManager, ILogger<PluginEventEmitter> logger) : base(hub)
    {
        _pluginManager = pluginManager;
        _logger = logger;
        _pluginManager.PluginInstalled += OnPluginInstalled;
        _pluginManager.PluginUninstalled += OnPluginUninstalled;
    }

    public void Dispose()
    {
        _pluginManager.PluginInstalled -= OnPluginInstalled;
        _pluginManager.PluginUninstalled -= OnPluginUninstalled;
    }

    private async void OnPluginInstalled(object? sender, PluginInstallationEventArgs e)
    {
        try
        {
            await SendAsync("installed", new PluginEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'installed' event.");
        }
    }

    private async void OnPluginUninstalled(object? sender, PluginInstallationEventArgs e)
    {
        try
        {
            await SendAsync("uninstalled", new PluginEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'uninstalled' event.");
        }
    }
}
