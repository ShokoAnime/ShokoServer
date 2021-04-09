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

        public AniDBHub(IUDPConnectionHandler udp, IHttpConnectionHandler http)
        {
            HttpHandler = http;
            UDPHandler = udp;
        }

        public override async Task OnConnectedAsync()
        {
            if (ServerState.Instance.DatabaseAvailable)
                await Clients.Caller.SendAsync("AniDBState", new Dictionary<string, object>
                {
                    {"UDPBanned", UDPHandler.IsBanned},
                    {"UDPBanTime", UDPHandler.BanTime},
                    {"UDPBanWaitPeriod", UDPHandler.BanTimerResetLength},
                    {"HttpBanned", HttpHandler.IsBanned},
                    {"HttpBanTime", HttpHandler.BanTime},
                    {"HttpBanWaitPeriod", HttpHandler.BanTimerResetLength},
                });
        }
    }
}
