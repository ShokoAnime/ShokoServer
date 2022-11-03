using System;
using System.Collections.Generic;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AniDBEmitter : BaseEmitter, IDisposable
{
    private IUDPConnectionHandler UDPHandler { get; set; }
    private IHttpConnectionHandler HttpHandler { get; set; }
    public event EventHandler<(string Name, AniDBStateUpdate State)> StateUpdate;

    public AniDBEmitter(IUDPConnectionHandler udp, IHttpConnectionHandler http)
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

    private void OnUDPStateUpdate(object sender, AniDBStateUpdate e)
    {
        StateUpdate?.Invoke(this, (GetName("AniDBUDPStateUpdate"), e));
    }

    private void OnHttpStateUpdate(object sender, AniDBStateUpdate e)
    {
        StateUpdate?.Invoke(this, (GetName("AniDBHttpStateUpdate"), e));
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
                PauseTimeSecs = TimeSpan.FromHours(UDPHandler.BanTimerResetLength).Seconds
            },
            new()
            {
                UpdateType = UpdateType.HTTPBan,
                UpdateTime = HttpHandler.BanTime ?? DateTime.Now,
                Value = HttpHandler.IsBanned,
                PauseTimeSecs = TimeSpan.FromHours(HttpHandler.BanTimerResetLength).Seconds
            }
        };
    }
}
