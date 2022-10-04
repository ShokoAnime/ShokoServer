using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.API.SignalR;

public class ShokoEventEmitter : IDisposable
{
    private IHubContext<ShokoEventHub> Hub { get; set; }
    private IShokoEventHandler EventHandler { get; set; }

    public ShokoEventEmitter(IHubContext<ShokoEventHub> hub, IShokoEventHandler events)
    {
        Hub = hub;
        EventHandler = events;
        EventHandler.FileDetected += OnFileDetected;
        EventHandler.FileHashed += OnFileHashed;
        EventHandler.FileMatched += OnFileMatched;
        EventHandler.SeriesUpdated += OnSeriesUpdated;
        EventHandler.EpisodeUpdated += OnEpisodeUpdated;
    }

    public void Dispose()
    {
        EventHandler.FileDetected -= OnFileDetected;
        EventHandler.FileHashed -= OnFileHashed;
        EventHandler.FileMatched -= OnFileMatched;
        EventHandler.SeriesUpdated -= OnSeriesUpdated;
        EventHandler.EpisodeUpdated -= OnEpisodeUpdated;
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileDetected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileHashedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileHashed", new FileHashedEventSignalRModel(e));
    }

    private async void OnFileMatched(object sender, FileMatchedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileMatched", new FileMatchedEventSignalRModel(e));
    }

    private async void OnSeriesUpdated(object sender, SeriesInfoUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("SeriesUpdated", new SeriesInfoUpdatedEventSignalRModel(e));
    }

    private async void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("EpisodeUpdated", new EpisodeInfoUpdatedEventSignalRModel(e));
    }
}
