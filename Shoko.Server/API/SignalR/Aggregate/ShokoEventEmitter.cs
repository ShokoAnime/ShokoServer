using System;
using Shoko.Plugin.Abstractions;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ShokoEventEmitter : BaseEmitter, IDisposable
{
    private IShokoEventHandler EventHandler { get; set; }
    public event EventHandler<(string Name, object args)> StateUpdate;

    public ShokoEventEmitter(IShokoEventHandler events)
    {
        EventHandler = events;
        EventHandler.FileDetected += OnFileDetected;
        EventHandler.FileHashed += OnFileHashed;
        EventHandler.FileMatched += OnFileMatched;
        EventHandler.FileDeleted += OnFileDeleted;
        EventHandler.SeriesUpdated += OnSeriesUpdated;
        EventHandler.EpisodeUpdated += OnEpisodeUpdated;
    }

    public void Dispose()
    {
        EventHandler.FileDetected -= OnFileDetected;
        EventHandler.FileHashed -= OnFileHashed;
        EventHandler.FileMatched -= OnFileMatched;
        EventHandler.FileDeleted -= OnFileDeleted;
        EventHandler.SeriesUpdated -= OnSeriesUpdated;
        EventHandler.EpisodeUpdated -= OnEpisodeUpdated;
    }

    private void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        StateUpdate?.Invoke(this, (GetName("FileDetected"), new FileDetectedEventSignalRModel(e)));
    }

    private void OnFileDeleted(object sender, FileDeletedEventArgs e)
    {
        StateUpdate?.Invoke(this, (GetName("FileDeleted"), new FileDeletedEventSignalRModel(e)));
    }

    private void OnFileHashed(object sender, FileHashedEventArgs e)
    {
        StateUpdate?.Invoke(this, (GetName("FileHashed"), new FileHashedEventSignalRModel(e)));
    }

    private void OnFileMatched(object sender, FileMatchedEventArgs e)
    {
        StateUpdate?.Invoke(this, (GetName("FileMatched"), new FileMatchedEventSignalRModel(e)));
    }

    private void OnSeriesUpdated(object sender, SeriesInfoUpdatedEventArgs e)
    {
        StateUpdate?.Invoke(this, (GetName("SeriesUpdated"), new SeriesInfoUpdatedEventSignalRModel(e)));
    }

    private void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        StateUpdate?.Invoke(this, (GetName("EpisodeUpdated"), new EpisodeInfoUpdatedEventSignalRModel(e)));
    }

    public override object GetInitialMessage()
    {
        // No back data for this
        return null;
    }
}
