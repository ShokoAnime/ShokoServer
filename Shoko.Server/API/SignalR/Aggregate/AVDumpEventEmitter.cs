using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AVDumpEventEmitter : BaseEventEmitter, IDisposable
{
    public AVDumpEventEmitter(IHubContext<AggregateHub> hub) : base(hub)
    {
        ShokoEventHandler.Instance.AVDumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        ShokoEventHandler.Instance.AVDumpEvent -= OnAVDumpEvent;
    }

    private async void OnAVDumpEvent(object sender, AvdumpEventArgs eventArgs)
    {
        await SendAsync("event", new AVDumpEventSignalRModel(eventArgs));
    }

    protected override object[] GetInitialMessages()
    {
        return [
            AVDumpHelper.GetActiveSessions()
                .Select(session => new AVDumpEventSignalRModel(session))
                .ToList()
        ];
    }
}
