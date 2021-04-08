using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Commons.Notification;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Commands;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR
{
    public class AniDBEmitter : IDisposable
    {
        private IHubContext<AniDBHub> Hub { get; set; }

        public AniDBEmitter(IHubContext<AniDBHub> hub)
        {
            Hub = hub;
            AniDBUDPConnectionHandler.Instance.AniDBStateUpdate += OnUDPStateUpdate;
            AniDBHttpConnectionHandler.Instance.AniDBStateUpdate += OnHttpStateUpdate;
        }

        public void Dispose()
        {
            AniDBUDPConnectionHandler.Instance.AniDBStateUpdate -= OnUDPStateUpdate;
            AniDBHttpConnectionHandler.Instance.AniDBStateUpdate -= OnHttpStateUpdate;
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
