using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.API.SignalR.Aggregate;

public class AnidbEventEmitter : BaseEventEmitter, IDisposable
{
    private IUDPConnectionHandler UDPHandler { get; set; }
    private IHttpConnectionHandler HttpHandler { get; set; }

    private readonly ILogger<AnidbEventEmitter> _logger;

    public AnidbEventEmitter(IHubContext<AggregateHub> hub, IUDPConnectionHandler udp, IHttpConnectionHandler http, ILogger<AnidbEventEmitter> logger) : base(hub)
    {
        HttpHandler = http;
        UDPHandler = udp;
        _logger = logger;
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
        try
        {
            await SendAsync("udp.stateUpdate", new AniDBStatusUpdateSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'udp.stateUpdate' event.");
        }
    }

    private async void OnHttpStateUpdate(object sender, AniDBStateUpdate e)
    {
        try
        {
            await SendAsync("http.stateUpdate", new AniDBStatusUpdateSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'http.stateUpdate' event.");
        }
    }

    protected override object[] GetInitialMessages()
    {
        return [
            new List<AniDBStatusUpdateSignalRModel>
            {
                new(UDPHandler.State),
                new(HttpHandler.State),
            },
        ];
    }
}
