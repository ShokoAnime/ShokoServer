using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class MetadataEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IMetadataService _metadataService;

    public MetadataEventEmitter(IHubContext<AggregateHub> hub, IMetadataService metadataService) : base(hub)
    {
        _metadataService = metadataService;
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

    private async void OnSeriesUpdated(object sender, SeriesInfoUpdatedEventArgs e)
    {
        var eventName = e.Reason is UpdateReason.None ? "series.updated" : "series." + e.Reason.ToString().ToLower();
        await SendAsync(eventName, new SeriesInfoUpdatedEventSignalRModel(e));
    }

    private async void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        var eventName = e.Reason is UpdateReason.None ? "episode.updated" : "episode." + e.Reason.ToString().ToLower();
        await SendAsync(eventName, new EpisodeInfoUpdatedEventSignalRModel(e));
    }

    private async void OnMovieUpdated(object sender, MovieInfoUpdatedEventArgs e)
    {
        var eventName = e.Reason is UpdateReason.None ? "movie.updated" : "movie." + e.Reason.ToString().ToLower();
        await SendAsync(eventName, new MovieInfoUpdatedEventSignalRModel(e));
    }
}
