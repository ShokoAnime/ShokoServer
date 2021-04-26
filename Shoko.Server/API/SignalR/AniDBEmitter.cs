using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.API.SignalR
{
    public class AniDBEmitter : IDisposable
    {
        private IHubContext<AniDBHub> Hub { get; set; }
        private IUDPConnectionHandler UDPHandler { get; set; }
        private IHttpConnectionHandler HttpHandler { get; set; }

        public AniDBEmitter(IHubContext<AniDBHub> hub, IUDPConnectionHandler udp, IHttpConnectionHandler http)
        {
            Hub = hub;
            HttpHandler = http;
            UDPHandler = udp;
            UDPHandler.AniDBStateUpdate += OnUDPStateUpdate;
            HttpHandler.AniDBStateUpdate += OnHttpStateUpdate;
        }

        public void Dispose()
        {
            UDPHandler.AniDBStateUpdate -= OnUDPStateUpdate;
            HttpHandler.AniDBStateUpdate -= OnHttpStateUpdate;
        }

        public async Task OnConnectedAsync(IClientProxy caller)
        {
            await caller.SendAsync("AniDBState", new Dictionary<string, object>
            {
                {"UDPBanned", UDPHandler.IsBanned},
                {"UDPBanTime", UDPHandler.BanTime},
                {"UDPBanWaitPeriod", UDPHandler.BanTimerResetLength},
                {"HttpBanned", HttpHandler.IsBanned},
                {"HttpBanTime", HttpHandler.BanTime},
                {"HttpBanWaitPeriod", HttpHandler.BanTimerResetLength},
            });
        }

        private async void OnUDPStateUpdate(object sender, AniDBStateUpdate e)
        {
            await Hub.Clients.All.SendAsync("AniDBUDPStateUpdate", e);
        }

        private async void OnHttpStateUpdate(object sender, AniDBStateUpdate e)
        {
            await Hub.Clients.All.SendAsync("AniDBHttpStateUpdate", e);
        }
    }
}
