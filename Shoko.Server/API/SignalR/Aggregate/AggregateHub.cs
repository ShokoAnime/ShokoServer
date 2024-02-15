using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Scheduling;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AggregateHub : Hub
{
    private readonly AniDBEmitter _aniDBEmitter;

    private readonly QueueEmitter _queueEmitter;

    private readonly ShokoEventEmitter _shokoEmitter;

    private readonly AVDumpEmitter _avdumpEmitter;

    private readonly NetworkEmitter _networkEmitter;
    private readonly QueueHandler _queueHandler;

    public AggregateHub(AniDBEmitter aniDBEmitter, QueueEmitter queueEmitter, ShokoEventEmitter shokoEmitter, AVDumpEmitter avdumpEmitter, NetworkEmitter networkEmitter, QueueHandler queueHandler)
    {
        _aniDBEmitter = aniDBEmitter;
        _queueEmitter = queueEmitter;
        _shokoEmitter = shokoEmitter;
        _avdumpEmitter = avdumpEmitter;
        _networkEmitter = networkEmitter;
        _queueHandler = queueHandler;
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
                    await Groups.AddToGroupAsync(Context.ConnectionId, _aniDBEmitter.Group);
                    await Clients.Caller.SendAsync(_aniDBEmitter.GetName(OnConnected), _aniDBEmitter.GetInitialMessage());
                    break;
                case "queue":
                    await Groups.AddToGroupAsync(Context.ConnectionId, _queueEmitter.Group);
                    await Clients.Caller.SendAsync(_queueEmitter.GetName(OnConnected), _queueEmitter.GetInitialMessage());
                    break;
                case "shoko":
                    await Groups.AddToGroupAsync(Context.ConnectionId, _shokoEmitter.Group);
                    await Clients.Caller.SendAsync(_shokoEmitter.GetName(OnConnected), _shokoEmitter.GetInitialMessage());
                    break;
                case "avdump":
                    await Groups.AddToGroupAsync(Context.ConnectionId, _avdumpEmitter.Group);
                    await Clients.Caller.SendAsync(_avdumpEmitter.GetName(OnConnected), _avdumpEmitter.GetInitialMessage());
                    break;
                case "network":
                    await Groups.AddToGroupAsync(Context.ConnectionId, _networkEmitter.Group);
                    await Clients.Caller.SendAsync(_networkEmitter.GetName(OnConnected), _networkEmitter.GetInitialMessage());
                    break;
            }
        }
    }

    public void ChangeQueueProcessingState(string queue, bool paused)
    {
        if (paused) _queueHandler.Pause().GetAwaiter().GetResult();
        else _queueHandler.Resume().GetAwaiter().GetResult();
    }
}
