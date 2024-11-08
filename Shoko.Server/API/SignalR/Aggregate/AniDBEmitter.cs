using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Shoko.Server.API.SignalR.Models;
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
        await SendAsync("AniDBUDPStateUpdate", new AniDBStatusUpdateSignalRModel(e));
    }

    private async void OnHttpStateUpdate(object sender, AniDBStateUpdate e)
    {
        await SendAsync("AniDBHttpStateUpdate", new AniDBStatusUpdateSignalRModel(e));
    }

    public override object GetInitialMessage()
    {
        return new List<AniDBStatusUpdateSignalRModel>
        {
            new(UpdateType.UDPBan, UDPHandler.IsBanned, UDPHandler.BanTime ?? DateTime.Now, (int)TimeSpan.FromHours(UDPHandler.BanTimerResetLength).TotalSeconds),
            new(UpdateType.HTTPBan, HttpHandler.IsBanned, HttpHandler.BanTime ?? DateTime.Now, (int)TimeSpan.FromHours(HttpHandler.BanTimerResetLength).TotalSeconds),
        };
    }
}
