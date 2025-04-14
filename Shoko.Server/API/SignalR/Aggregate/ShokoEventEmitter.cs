using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class ShokoEventEmitter : BaseEmitter, IDisposable
{
    private readonly IMetadataService _metadataService;

    private readonly IVideoService _videoService;

    public ShokoEventEmitter(IHubContext<AggregateHub> hub, IMetadataService metadataService, IVideoService videoService) : base(hub)
    {
        _metadataService = metadataService;
        _videoService = videoService;
        _videoService.VideoFileDetected += OnFileDetected;
        _videoService.VideoFileHashed += OnFileHashed;
        _videoService.VideoFileRelocated += OnFileRelocated;
        _videoService.VideoFileDeleted += OnFileDeleted;
        _metadataService.SeriesAdded += OnSeriesUpdated;
        _metadataService.SeriesUpdated += OnSeriesUpdated;
        _metadataService.SeriesRemoved += OnSeriesUpdated;
        _metadataService.EpisodeAdded += OnEpisodeUpdated;
        _metadataService.EpisodeUpdated += OnEpisodeUpdated;
        _metadataService.EpisodeRemoved += OnEpisodeUpdated;
        _metadataService.MovieAdded += OnMovieUpdated;
        _metadataService.MovieUpdated += OnMovieUpdated;
        _metadataService.MovieRemoved += OnMovieUpdated;
    }

    public void Dispose()
    {
        _videoService.VideoFileDetected -= OnFileDetected;
        _videoService.VideoFileHashed -= OnFileHashed;
        _videoService.VideoFileRelocated -= OnFileRelocated;
        _videoService.VideoFileDeleted -= OnFileDeleted;
        _metadataService.SeriesAdded -= OnSeriesUpdated;
        _metadataService.SeriesUpdated -= OnSeriesUpdated;
        _metadataService.SeriesRemoved -= OnSeriesUpdated;
        _metadataService.EpisodeAdded -= OnEpisodeUpdated;
        _metadataService.EpisodeUpdated -= OnEpisodeUpdated;
        _metadataService.EpisodeRemoved -= OnEpisodeUpdated;
        _metadataService.MovieAdded -= OnMovieUpdated;
        _metadataService.MovieUpdated -= OnMovieUpdated;
        _metadataService.MovieRemoved -= OnMovieUpdated;
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await SendAsync("File.Detected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileDeleted(object sender, FileEventArgs e)
    {
        await SendAsync("File.Deleted", new FileEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileEventArgs e)
    {
        await SendAsync("File.Hashed", new FileEventSignalRModel(e));
    }

    private async void OnFileRelocated(object sender, FileRelocatedEventArgs e)
    {
        await SendAsync("File.Relocated", new FileRelocatedEventSignalRModel(e));
    }

    private async void OnSeriesUpdated(object sender, SeriesInfoUpdatedEventArgs e)
    {
        var eventName = e.Reason is UpdateReason.None ? "Series.Updated" : "Series." + e.Reason.ToString();
        await SendAsync(eventName, new SeriesInfoUpdatedEventSignalRModel(e));
    }

    private async void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        var eventName = e.Reason is UpdateReason.None ? "Episode.Updated" : "Episode." + e.Reason.ToString();
        await SendAsync(eventName, new EpisodeInfoUpdatedEventSignalRModel(e));
    }

    private async void OnMovieUpdated(object sender, MovieInfoUpdatedEventArgs e)
    {
        var eventName = e.Reason is UpdateReason.None ? "Movie.Updated" : "Movie." + e.Reason.ToString();
        await SendAsync(eventName, new MovieInfoUpdatedEventSignalRModel(e));
    }

    public override object GetInitialMessage()
    {
        // No back data for this
        return null;
    }
}
