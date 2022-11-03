// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AggregateHub : Hub
{
    private AniDBEmitter AniDBEmitter { get; set; }
    private QueueEmitter QueueEmitter { get; set; }
    private ShokoEventEmitter ShokoEmitter { get; set; }
    
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var context = Context.GetHttpContext();
        var query = context?.Request.Query["feeds"].SelectMany(a => a.Split(","))
            .Select(a => a.ToLower().Trim());
        if (query == null) return;
        var services = context.RequestServices;
        const string OnConnected = "OnConnected";

        foreach (var feed in query)
        {
            switch (feed)
            {
                case "anidb":
                    AniDBEmitter = services.GetRequiredService<AniDBEmitter>();
                    await Clients.All.SendAsync(AniDBEmitter.GetName(OnConnected), AniDBEmitter.GetInitialMessage());
                    break;
                case "queue":
                    QueueEmitter = services.GetRequiredService<QueueEmitter>();
                    await Clients.All.SendAsync(QueueEmitter.GetName(OnConnected), QueueEmitter.GetInitialMessage());
                    break;
                case "shoko":
                    ShokoEmitter = services.GetRequiredService<ShokoEventEmitter>();
                    await Clients.All.SendAsync(ShokoEmitter.GetName(OnConnected), ShokoEmitter.GetInitialMessage());
                    break;
            }
        }
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
}
