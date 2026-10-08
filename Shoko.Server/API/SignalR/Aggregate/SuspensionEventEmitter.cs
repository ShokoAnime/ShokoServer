using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Server.API.v3.Models.Suspension;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The <c>suspension</c> feed: why each service the server talks to cannot
/// take work. Joining sends every provider's status as
/// <c>suspension:connected</c>, and each change sends the changed provider's
/// status as <c>suspension:changed</c>. Open to every authenticated user.
/// </summary>
public class SuspensionEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "suspension";

    private readonly ISuspensionService _suspensionService;

    private readonly ILogger<SuspensionEventEmitter> _logger;

    public SuspensionEventEmitter(IHubContext<AggregateHub> hub, ISuspensionService suspensionService, ILogger<SuspensionEventEmitter> logger) : base(hub)
    {
        _suspensionService = suspensionService;
        _logger = logger;
        _suspensionService.SuspensionChanged += OnSuspensionChanged;
    }

    public void Dispose()
    {
        _suspensionService.SuspensionChanged -= OnSuspensionChanged;
    }

    private async void OnSuspensionChanged(object? sender, SuspensionChangedEventArgs eventArgs)
    {
        try
        {
            await SendAsync("changed", new SuspensionProviderStatus(eventArgs.Current));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'changed' event.");
        }
    }

    protected override object[] GetInitialMessages()
        => [_suspensionService.GetAll().Select(status => new SuspensionProviderStatus(status)).ToList()];
}
