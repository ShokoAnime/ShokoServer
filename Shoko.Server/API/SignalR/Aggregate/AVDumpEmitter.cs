using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AVDumpEmitter : BaseEmitter, IDisposable
{
    public AVDumpEmitter(IHubContext<AggregateHub> hub) : base(hub)
    {
        ShokoEventHandler.Instance.AVDumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        ShokoEventHandler.Instance.AVDumpEvent -= OnAVDumpEvent;
    }

    private async void OnAVDumpEvent(object sender, AVDumpEventArgs eventArgs)
    {
        await SendAsync("Event", new AVDumpEventSignalRModel(eventArgs));
    }

    public override object GetInitialMessage()
    {
        return AVDumpHelper.GetActiveSessions()
            .Select(session => new AVDumpEventSignalRModel(session))
            .ToList();
    }
}
