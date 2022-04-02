using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Server.AniDB_API;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.API.SignalR
{
    public class LegacyAniDBEmitter : IDisposable
    {
        private IHubContext<LegacyAniDBHub> Hub { get; }
        private AniDBHelper AniDBHelper { get; }

        public LegacyAniDBEmitter(IHubContext<LegacyAniDBHub> hub, AniDBHelper helper)
        {
            Hub = hub;
            AniDBHelper = helper;
            helper.AniDBStateUpdate += OnStateUpdate;
        }

        public void Dispose()
        {
            AniDBHelper.AniDBStateUpdate -= OnStateUpdate;
        }

        public async Task OnConnectedAsync(IClientProxy caller)
        {
            await caller.SendAsync("AniDBState", new Dictionary<string, object>
            {
                {"UDPBanned", AniDBHelper.IsUdpBanned},
                {"UDPBanTime", AniDBHelper.UdpBanTime},
                {"UDPBanWaitPeriod", AniDBHelper.UDPBanTimerResetLength},
                {"HttpBanned", AniDBHelper.IsHttpBanned},
                {"HttpBanTime", AniDBHelper.HttpBanTime},
                {"HttpBanWaitPeriod", AniDBHelper.HTTPBanTimerResetLength},
            });
        }

        private async void OnStateUpdate(object sender, AniDBStateUpdate e)
        {
            switch (e.UpdateType)
            {
                case UpdateType.UDPBan:
                    await Hub.Clients.All.SendAsync("AniDBUDPStateUpdate", e);
                    break;
                case UpdateType.HTTPBan:
                    await Hub.Clients.All.SendAsync("AniDBHttpStateUpdate", e);
                    break;
            }
        }
    }
}
