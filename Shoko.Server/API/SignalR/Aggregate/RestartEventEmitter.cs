using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The <c>restart</c> feed: every reason the server needs a restart, for admins only. Joining sends
/// the current reasons as <c>restart:connected</c>, and each change sends them all again as
/// <c>restart:reasonsChanged</c>.
/// </summary>
public class RestartEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "restart";

    private readonly ISystemService _systemService;

    private readonly IPluginManager _pluginManager;

    private readonly ILogger<RestartEventEmitter> _logger;

    public RestartEventEmitter(
        IHubContext<AggregateHub> hub,
        ISystemService systemService,
        IPluginManager pluginManager,
        ILogger<RestartEventEmitter> logger
    ) : base(hub)
    {
        _systemService = systemService;
        _pluginManager = pluginManager;
        _logger = logger;
        _systemService.RestartReasonsChanged += OnRestartReasonsChanged;
    }

    public void Dispose()
    {
        _systemService.RestartReasonsChanged -= OnRestartReasonsChanged;
    }

    private async void OnRestartReasonsChanged(object? sender, RestartReasonsChangedEventArgs eventArgs)
    {
        try
        {
            await SendAsync("reasonsChanged", new RestartReasonsSignalRModel(eventArgs.Reasons, _pluginManager));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'reasonsChanged' event.");
        }
    }

    protected override bool CanConnect(IUser user)
        => user.IsAdmin;

    protected override object[] GetInitialMessages()
        => [new RestartReasonsSignalRModel(_systemService.RestartReasons, _pluginManager)];
}
