using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AniDBEmitter : BaseEmitter, IDisposable
{
    private IUDPConnectionHandler UDPHandler { get; set; }
    private IHttpConnectionHandler HttpHandler { get; set; }

    public AniDBEmitter(IHubContext<AggregateHub> hub, IUDPConnectionHandler udp, IHttpConnectionHandler http) : base(hub)
    {
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

    private async void OnUDPStateUpdate(object sender, AniDBStateUpdate e)
    {
        await SendAsync("AniDBUDPStateUpdate", e);
    }

    private async void OnHttpStateUpdate(object sender, AniDBStateUpdate e)
    {
        await SendAsync("AniDBHttpStateUpdate", e);
    }

    public override object GetInitialMessage()
    {
        return new List<AniDBStateUpdate>
        {
            new()
            {
                UpdateType = UpdateType.UDPBan,
                UpdateTime = UDPHandler.BanTime ?? DateTime.Now,
                Value = UDPHandler.IsBanned,
                PauseTimeSecs = (int)TimeSpan.FromHours(UDPHandler.BanTimerResetLength).TotalSeconds
            },
            new()
            {
                UpdateType = UpdateType.HTTPBan,
                UpdateTime = HttpHandler.BanTime ?? DateTime.Now,
                Value = HttpHandler.IsBanned,
                PauseTimeSecs = (int)TimeSpan.FromHours(HttpHandler.BanTimerResetLength).TotalSeconds
            }
        };
    }
}
