using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR.Legacy;

public class ShokoEventHub : Hub
{
    private ShokoEventEmitter _shokoEventEmitter { get; set; }

    public ShokoEventHub(ShokoEventEmitter emitter)
    {
        _shokoEventEmitter = emitter;
    }
}
