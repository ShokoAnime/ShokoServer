using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ShokoEventEmitter : BaseEmitter, IDisposable
{
    private IShokoEventHandler EventHandler { get; set; }

    public ShokoEventEmitter(IHubContext<AggregateHub> hub, IShokoEventHandler events) : base(hub)
    {
        EventHandler = events;
        EventHandler.FileDetected += OnFileDetected;
        EventHandler.FileHashed += OnFileHashed;
        EventHandler.FileMatched += OnFileMatched;
        EventHandler.FileRenamed += OnFileRenamed;
        EventHandler.FileMoved += OnFileMoved;
        EventHandler.FileNotMatched += OnFileNotMatched;
        EventHandler.FileDeleted += OnFileDeleted;
        EventHandler.SeriesUpdated += OnSeriesUpdated;
        EventHandler.EpisodeUpdated += OnEpisodeUpdated;
        EventHandler.MovieUpdated += OnMovieUpdated;
    }

    public void Dispose()
    {
        EventHandler.FileDetected -= OnFileDetected;
        EventHandler.FileHashed -= OnFileHashed;
        EventHandler.FileMatched -= OnFileMatched;
        EventHandler.FileNotMatched -= OnFileNotMatched;
        EventHandler.FileRenamed -= OnFileRenamed;
        EventHandler.FileMoved -= OnFileMoved;
        EventHandler.FileDeleted -= OnFileDeleted;
        EventHandler.SeriesUpdated -= OnSeriesUpdated;
        EventHandler.EpisodeUpdated -= OnEpisodeUpdated;
        EventHandler.MovieUpdated -= OnMovieUpdated;
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await SendAsync("FileDetected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileDeleted(object sender, FileEventArgs e)
    {
        await SendAsync("FileDeleted", new FileEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileEventArgs e)
    {
        await SendAsync("FileHashed", new FileEventSignalRModel(e));
    }

    private async void OnFileMatched(object sender, FileEventArgs e)
    {
        await SendAsync("FileMatched", new FileEventSignalRModel(e));
    }

    private async void OnFileRenamed(object sender, FileRenamedEventArgs e)
    {
        await SendAsync("FileRenamed", new FileRenamedEventSignalRModel(e));
    }

    private async void OnFileMoved(object sender, FileMovedEventArgs e)
    {
        await SendAsync("FileMoved", new FileMovedEventSignalRModel(e));
    }

    private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs e)
    {
        await SendAsync("FileNotMatched", new FileNotMatchedEventSignalRModel(e));
    }

    private async void OnSeriesUpdated(object sender, SeriesInfoUpdatedEventArgs e)
    {
        await SendAsync("SeriesUpdated", new SeriesInfoUpdatedEventSignalRModel(e));
    }

    private async void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        await SendAsync("EpisodeUpdated", new EpisodeInfoUpdatedEventSignalRModel(e));
    }

    private async void OnMovieUpdated(object sender, MovieInfoUpdatedEventArgs e)
    {
        await SendAsync("MovieUpdated", new MovieInfoUpdatedEventSignalRModel(e));
    }

    public override object GetInitialMessage()
    {
        // No back data for this
        return null;
    }
}
