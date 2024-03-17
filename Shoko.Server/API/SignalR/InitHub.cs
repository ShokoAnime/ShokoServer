using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR;

public class InitHub : Hub
{
    private InitEmitter Emitter { get; set; }

    public InitHub(InitEmitter emitter)
    {
        Emitter = emitter;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, InitEmitter.Group);
        await Emitter.OnConnectedAsync(Clients.Caller);
    }
}
