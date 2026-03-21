using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AvdumpEventEmitter : BaseEventEmitter, IDisposable
{
    public AvdumpEventEmitter(IHubContext<AggregateHub> hub) : base(hub)
    {
        ShokoEventHandler.Instance.AvdumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        ShokoEventHandler.Instance.AvdumpEvent -= OnAVDumpEvent;
    }

    private async void OnAVDumpEvent(object sender, AnidbAvdumpEventArgs eventArgs)
    {
        await SendAsync("event", new AvdumpEventSignalRModel(eventArgs));
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
