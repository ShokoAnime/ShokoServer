using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class AniDBHub : Hub
    {
        private IUDPConnectionHandler UDPHandler { get; set; }
        private IHttpConnectionHandler HttpHandler { get; set; }
        private AniDBEmitter _aniDBEmitter { get; set; }

        public AniDBHub(IUDPConnectionHandler udp, IHttpConnectionHandler http, AniDBEmitter emitter)
        {
            HttpHandler = http;
            UDPHandler = udp;
            _aniDBEmitter = emitter;
        }

        public override async Task OnConnectedAsync()
        {
            if (ServerState.Instance.DatabaseAvailable)
                await _aniDBEmitter.OnConnectedAsync(Clients.Caller);
        }
    }
}
