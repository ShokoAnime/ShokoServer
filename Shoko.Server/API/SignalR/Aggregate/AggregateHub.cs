// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AggregateHub : Hub
{
    private readonly AniDBEmitter _aniDBEmitter;
    private readonly QueueEmitter _queueEmitter;
    private readonly ShokoEventEmitter _shokoEmitter;

    public AggregateHub(AniDBEmitter aniDBEmitter, QueueEmitter queueEmitter, ShokoEventEmitter shokoEmitter)
    {
        _aniDBEmitter = aniDBEmitter;
        _queueEmitter = queueEmitter;
        _shokoEmitter = shokoEmitter;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var context = Context.GetHttpContext();
        var query = context?.Request.Query["feeds"].SelectMany(a => a.Split(","))
            .Select(a => a.ToLower().Trim());
        if (query == null) return;
        const string OnConnected = "OnConnected";

        foreach (var feed in query)
        {
            switch (feed)
            {
                case "anidb":
                    _aniDBEmitter.StateUpdate += AniDBOnStateUpdate;
                    await Clients.All.SendAsync(_aniDBEmitter.GetName(OnConnected), _aniDBEmitter.GetInitialMessage());
                    break;
                case "queue":
                    _queueEmitter.StateUpdate += QueueOnStateUpdate;
                    await Clients.All.SendAsync(_queueEmitter.GetName(OnConnected), _queueEmitter.GetInitialMessage());
                    break;
                case "shoko":
                    _shokoEmitter.StateUpdate += ShokoOnStateUpdate;
                    await Clients.All.SendAsync(_shokoEmitter.GetName(OnConnected), _shokoEmitter.GetInitialMessage());
                    break;
            }
        }
    }

    private async void AniDBOnStateUpdate(object sender, (string Message, AniDBStateUpdate State) e)
    {
        await Clients.All.SendAsync(e.Message, e.State);
    }

    private async void QueueOnStateUpdate(object sender, (string Message, object[] args) e)
    {
        await Clients.All.SendCoreAsync(e.Message, e.args);
    }

    private async void ShokoOnStateUpdate(object sender, (string Message, object State) e)
    {
        await Clients.All.SendAsync(e.Message, e.State);
    }

    public void ChangeQueueProcessingState(string queue, bool paused)
    {
        switch (queue.ToLower())
        {
            case "general":
                ShokoService.CmdProcessorGeneral.Paused = paused;
                break;
            case "hasher":
                ShokoService.CmdProcessorHasher.Paused = paused;
                break;
            case "images":
                ShokoService.CmdProcessorImages.Paused = paused;
                break;
        }
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _aniDBEmitter.StateUpdate -= AniDBOnStateUpdate;
        _queueEmitter.StateUpdate -= QueueOnStateUpdate;
        _shokoEmitter.StateUpdate -= ShokoOnStateUpdate;
        await base.OnDisconnectedAsync(exception);
    }

    protected override void Dispose(bool disposing)
    {
        // disposed before disconnecting?
        _aniDBEmitter.StateUpdate -= AniDBOnStateUpdate;
        _queueEmitter.StateUpdate -= QueueOnStateUpdate;
        _shokoEmitter.StateUpdate -= ShokoOnStateUpdate;
        base.Dispose(disposing);
    }
}
