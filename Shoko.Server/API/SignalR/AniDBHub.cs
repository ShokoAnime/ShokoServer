using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class AniDBHub : Hub
    {
        private readonly AniDBEmitter _eventEmitter;

        public AniDBHub(AniDBEmitter eventEmitter)
        {
            _eventEmitter = eventEmitter;
        }

        public override async Task OnConnectedAsync()
        {
            if (ServerState.Instance.DatabaseAvailable)
                await Clients.Caller.SendAsync("AniDBState", new Dictionary<string, object>
                {
                    {"UDPBanned", AniDBUDPConnectionHandler.Instance.IsBanned},
                    {"UDPBanTime", AniDBUDPConnectionHandler.Instance.BanTime},
                    {"UDPBanWaitPeriod", AniDBUDPConnectionHandler.BanTimerResetLength},
                    {"HttpBanned", AniDBHttpConnectionHandler.Instance.IsBanned},
                    {"HttpBanTime", AniDBHttpConnectionHandler.Instance.BanTime},
                    {"HttpBanWaitPeriod", AniDBHttpConnectionHandler.BanTimerResetLength},
                });
        }
    }
}
