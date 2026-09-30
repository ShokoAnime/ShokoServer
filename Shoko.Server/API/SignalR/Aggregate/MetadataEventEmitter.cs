using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

public class MetadataEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "metadata";

    private readonly IMetadataService _metadataService;

    private readonly IMetadataLinkingService _linkingService;

    private readonly ILogger<MetadataEventEmitter> _logger;

    public MetadataEventEmitter(
        IHubContext<AggregateHub> hub,
        IMetadataService metadataService,
        IMetadataLinkingService linkingService,
        ILogger<MetadataEventEmitter> logger
    ) : base(hub)
    {
        _metadataService = metadataService;
        _linkingService = linkingService;
        _logger = logger;
        _linkingService.LinksChanged += OnLinksChanged;
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
        _linkingService.LinksChanged -= OnLinksChanged;
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

    private async void OnSeriesUpdated(object? sender, SeriesInfoUpdatedEventArgs e)
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

    private async void OnEpisodeUpdated(object? sender, EpisodeInfoUpdatedEventArgs e)
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

    private async void OnLinksChanged(object? sender, MetadataLinksChangedEventArgs e)
    {
        try
        {
            var shokoSeriesIDs = e.AnidbAnimeIDs
                .Select(_metadataService.GetShokoSeriesByAnidbID)
                .OfType<IShokoSeries>()
                .Select(series => series.LocalID)
                .Order()
                .ToList();
            await SendAsync("links.changed", new MetadataLinksChangedSignalRModel(e, shokoSeriesIDs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'links.changed' event.");
        }
    }

    private async void OnMovieUpdated(object? sender, MovieInfoUpdatedEventArgs e)
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
