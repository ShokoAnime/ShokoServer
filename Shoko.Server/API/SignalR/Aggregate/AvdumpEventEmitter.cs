using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AvdumpEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly ILogger<AvdumpEventEmitter> _logger;

    public AvdumpEventEmitter(IHubContext<AggregateHub> hub, ILogger<AvdumpEventEmitter> logger) : base(hub)
    {
        _logger = logger;
        ShokoEventHandler.Instance.AvdumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        ShokoEventHandler.Instance.AvdumpEvent -= OnAVDumpEvent;
    }

    private async void OnAVDumpEvent(object sender, AnidbAvdumpEventArgs eventArgs)
    {
        try
        {
            await SendAsync("event", new AvdumpEventSignalRModel(eventArgs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'event' event.");
        }
    }

    protected override object[] GetInitialMessages()
    {
        return [
            AVDumpHelper.GetActiveSessions()
                .Select(session => new AvdumpEventSignalRModel(session))
                .ToList()
        ];
    }
}
