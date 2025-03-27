using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ShokoEventEmitter : BaseEmitter, IDisposable
{
    private IShokoEventHandler EventHandler { get; set; }

    private IVideoHashingService VideoHashingService { get; set; }

    public ShokoEventEmitter(IHubContext<AggregateHub> hub, IShokoEventHandler events, IVideoHashingService videoHashingService) : base(hub)
    {
        EventHandler = events;
        VideoHashingService = videoHashingService;
        EventHandler.FileDetected += OnFileDetected;
        VideoHashingService.FileHashed += OnFileHashed;
        EventHandler.FileRenamed += OnFileRenamed;
        EventHandler.FileMoved += OnFileMoved;
        EventHandler.FileDeleted += OnFileDeleted;
        EventHandler.SeriesUpdated += OnSeriesUpdated;
        EventHandler.EpisodeUpdated += OnEpisodeUpdated;
        EventHandler.MovieUpdated += OnMovieUpdated;
    }

    public void Dispose()
    {
        EventHandler.FileDetected -= OnFileDetected;
        VideoHashingService.FileHashed -= OnFileHashed;
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

    private async void OnFileRenamed(object sender, FileRenamedEventArgs e)
    {
        await SendAsync("FileRenamed", new FileRenamedEventSignalRModel(e));
    }

    private async void OnFileMoved(object sender, FileMovedEventArgs e)
    {
        await SendAsync("FileMoved", new FileMovedEventSignalRModel(e));
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
