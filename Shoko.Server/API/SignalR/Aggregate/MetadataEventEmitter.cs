using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the metadata and link events to the aggregate hub. An entry's event
/// reaches the users who may see the entry, by the rule of the metadata entry
/// routes: only AniDB and Shoko entries are kept from users.
/// </summary>
/// <remarks>
/// An AniDB or Shoko entry being removed may have lost what it belongs to, so
/// its removal goes to users without restricted tags, or to those who may still
/// be shown its anime. A link event is narrowed per user to the changes of the
/// anime the user may see, and an anime no longer known is hidden.
/// </remarks>
public class MetadataEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "metadata";

    private readonly IMetadataService _metadataService;

    private readonly IMetadataLinkingService _linkingService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly ILogger<MetadataEventEmitter> _logger;

    public MetadataEventEmitter(
        IHubContext<AggregateHub> hub,
        IMetadataService metadataService,
        IMetadataLinkingService linkingService,
        AniDB_AnimeRepository anidbAnime,
        ILogger<MetadataEventEmitter> logger
    ) : base(hub)
    {
        _metadataService = metadataService;
        _linkingService = linkingService;
        _anidbAnime = anidbAnime;
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
            var audience = EventAudience.ForEntry(e.SeriesInfo, _anidbAnime.GetByAnimeID, e.Reason is UpdateReason.Removed);
            await SendToAudienceAsync(audience, eventName, new SeriesInfoUpdatedEventSignalRModel(e));
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
            var audience = EventAudience.ForEntry(e.EpisodeInfo, _anidbAnime.GetByAnimeID, e.Reason is UpdateReason.Removed);
            await SendToAudienceAsync(audience, eventName, new EpisodeInfoUpdatedEventSignalRModel(e));
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
            var shokoSeriesIDs = new Dictionary<int, int>();
            var audiences = new Dictionary<int, EventAudience>();
            foreach (var animeID in e.AnidbAnimeIDs)
            {
                audiences[animeID] = EventAudience.ForAnime(animeID, _anidbAnime.GetByAnimeID);
                if (_metadataService.GetShokoSeriesByAnidbID(animeID) is IShokoSeries series)
                    shokoSeriesIDs[animeID] = series.LocalID;
            }

            await SendVisiblePartsAsync(
                "links.changed",
                e.Changes,
                [.. e.Changes.Select(change => audiences[change.AnidbAnimeID])],
                changes => new MetadataLinksChangedSignalRModel(
                    e.Reason,
                    changes,
                    [.. changes.Select(change => change.AnidbAnimeID).Distinct().Where(shokoSeriesIDs.ContainsKey).Select(animeID => shokoSeriesIDs[animeID]).Order()]
                )
            );
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
            var audience = EventAudience.ForEntry(e.MovieInfo, _anidbAnime.GetByAnimeID, e.Reason is UpdateReason.Removed);
            await SendToAudienceAsync(audience, eventName, new MovieInfoUpdatedEventSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'movie' event.");
        }
    }
}
