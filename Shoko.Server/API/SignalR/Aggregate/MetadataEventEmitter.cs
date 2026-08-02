using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class MetadataEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IMetadataService _metadataService;

    private readonly ILogger<MetadataEventEmitter> _logger;

    public MetadataEventEmitter(IHubContext<AggregateHub> hub, IMetadataService metadataService, ILogger<MetadataEventEmitter> logger) : base(hub)
    {
        _metadataService = metadataService;
        _logger = logger;
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
        try
        {
            var eventName = e.Reason is UpdateReason.None ? "series.updated" : "series." + e.Reason.ToString().ToLower();
            await SendAsync(eventName, new SeriesInfoUpdatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'series' event.");
        }
    }

    private async void OnEpisodeUpdated(object sender, EpisodeInfoUpdatedEventArgs e)
    {
        try
        {
            var eventName = e.Reason is UpdateReason.None ? "episode.updated" : "episode." + e.Reason.ToString().ToLower();
            await SendAsync(eventName, new EpisodeInfoUpdatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'episode' event.");
        }
    }

    private async void OnMovieUpdated(object sender, MovieInfoUpdatedEventArgs e)
    {
        try
        {
            var eventName = e.Reason is UpdateReason.None ? "movie.updated" : "movie." + e.Reason.ToString().ToLower();
            await SendAsync(eventName, new MovieInfoUpdatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'movie' event.");
        }
    }
}
