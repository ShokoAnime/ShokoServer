using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Legacy;

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
        EventHandler.FileRenamed += OnFileRenamed;
        EventHandler.FileMoved += OnFileMoved;
        EventHandler.FileDeleted += OnFileDeleted;
        EventHandler.FileNotMatched += OnFileNotMatched;
        EventHandler.SeriesUpdated += OnSeriesUpdated;
        EventHandler.EpisodeUpdated += OnEpisodeUpdated;
        EventHandler.MovieUpdated += OnMovieUpdated;
    }

    public void Dispose()
    {
        EventHandler.FileDetected -= OnFileDetected;
        EventHandler.FileHashed -= OnFileHashed;
        EventHandler.FileMatched -= OnFileMatched;
        EventHandler.FileRenamed -= OnFileRenamed;
        EventHandler.FileMoved -= OnFileMoved;
        EventHandler.FileDeleted -= OnFileDeleted;
        EventHandler.FileNotMatched -= OnFileNotMatched;
        EventHandler.SeriesUpdated -= OnSeriesUpdated;
        EventHandler.EpisodeUpdated -= OnEpisodeUpdated;
        EventHandler.MovieUpdated -= OnMovieUpdated;
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileDetected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileDeleted(object sender, FileEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileDeleted", new FileEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileHashed", new FileEventSignalRModel(e));
    }

    private async void OnFileMatched(object sender, FileEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileMatched", new FileEventSignalRModel(e));
    }

    private async void OnFileRenamed(object sender, FileRenamedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileRenamed", new FileRenamedEventSignalRModel(e));
    }

    private async void OnFileMoved(object sender, FileMovedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileMoved", new FileMovedEventSignalRModel(e));
    }

    private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileNotMatched", new FileNotMatchedEventSignalRModel(e));
    }

    private async void OnSeriesUpdated(object sender, SeriesInfoUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("SeriesUpdated", new SeriesInfoUpdatedEventSignalRModel(e));
    }

    private async void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("EpisodeUpdated", new EpisodeInfoUpdatedEventSignalRModel(e));
    }

    private async void OnMovieUpdated(object sender, MovieInfoUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("MovieUpdated", new MovieInfoUpdatedEventSignalRModel(e));
    }
}
