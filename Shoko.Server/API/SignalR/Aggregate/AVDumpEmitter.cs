using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AVDumpEmitter : BaseEmitter, IDisposable
{
    private IShokoEventHandler EventHandler { get; set; }

    public AVDumpEmitter(IHubContext<AggregateHub> hub, IShokoEventHandler events) : base(hub)
    {
        EventHandler = events;
        EventHandler.AVDumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        EventHandler.AVDumpEvent -= OnAVDumpEvent;
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
