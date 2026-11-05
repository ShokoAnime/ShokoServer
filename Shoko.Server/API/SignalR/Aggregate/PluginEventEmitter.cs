using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
///   Emits SignalR events for plugins being installed and uninstalled, and
///   for package installs being queued, starting, failing and being cancelled.
/// </summary>
public class PluginEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "plugin";

    private readonly IPluginManager _pluginManager;

    private readonly IPluginPackageManager _packageManager;

    private readonly ILogger<PluginEventEmitter> _logger;

    public PluginEventEmitter(IHubContext<AggregateHub> hub, IPluginManager pluginManager, IPluginPackageManager packageManager, ILogger<PluginEventEmitter> logger) : base(hub)
    {
        _pluginManager = pluginManager;
        _packageManager = packageManager;
        _logger = logger;
        _pluginManager.PluginInstalled += OnPluginInstalled;
        _pluginManager.PluginUninstalled += OnPluginUninstalled;
        _packageManager.PackageInstallationQueued += OnPackageInstallationQueued;
        _packageManager.PackageInstallationStarted += OnPackageInstallationStarted;
        _packageManager.PackageInstallationFailed += OnPackageInstallationFailed;
        _packageManager.PackageInstallationCancelled += OnPackageInstallationCancelled;
    }

    public void Dispose()
    {
        _pluginManager.PluginInstalled -= OnPluginInstalled;
        _pluginManager.PluginUninstalled -= OnPluginUninstalled;
        _packageManager.PackageInstallationQueued -= OnPackageInstallationQueued;
        _packageManager.PackageInstallationStarted -= OnPackageInstallationStarted;
        _packageManager.PackageInstallationFailed -= OnPackageInstallationFailed;
        _packageManager.PackageInstallationCancelled -= OnPackageInstallationCancelled;
    }

    private void OnPackageInstallationQueued(object? sender, PackageInstallationQueuedEventArgs e)
        => Send("install-queued", new PackageInstallSignalRModel(e.Entry.ID, e.Entry.Package, e.Entry.QueuedAt));

    // Raised inside the install, so the send goes off on its own.
    private void OnPackageInstallationStarted(object? sender, PackageInstallationStartedEventArgs e)
        => Send("install-started", new PackageInstallSignalRModel(e.QueueEntryID, e.Package, e.StartedAt));

    private void OnPackageInstallationFailed(object? sender, PackageInstallationFailedEventArgs e)
        => Send("install-failed", new PackageInstallSignalRModel(e.QueueEntryID, e.Package, e.FailedAt, e.Reason));

    private void OnPackageInstallationCancelled(object? sender, PackageInstallationCancelledEventArgs e)
        => Send("install-cancelled", new PackageInstallSignalRModel(e.Entry.ID, e.Entry.Package, e.CancelledAt));

    private void Send(string eventName, PackageInstallSignalRModel model)
        => _ = Task.Run(async () =>
        {
            try
            {
                await SendAsync(eventName, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending the '{EventName}' event.", eventName);
            }
        });

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
