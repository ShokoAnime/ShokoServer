using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
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
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await SendAsync("FileDetected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileDeleted(object sender, FileDeletedEventArgs e)
    {
        await SendAsync("FileDeleted", new FileDeletedEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileHashedEventArgs e)
    {
        await SendAsync("FileHashed", new FileHashedEventSignalRModel(e));
    }

    private async void OnFileMatched(object sender, FileMatchedEventArgs e)
    {
        await SendAsync("FileMatched", new FileMatchedEventSignalRModel(e));
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

    public override object GetInitialMessage()
    {
        // No back data for this
        return null;
    }
}
