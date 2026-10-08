using System;
using System.Collections.Generic;
using System.Threading;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services.Airing;

/// <summary>
/// The dates of every AniDB anime's stored airings, as
/// <see cref="AiringScheduleService.BuildAnidbAiringDates"/> gathers them.
/// </summary>
/// <remarks>
/// Built on the first read, then kept. A change to the airings, the
/// schedules, the providers, the links or any series or episode marks it
/// stale, and the next read builds it again. An anime whose dates did not
/// change keeps the same instance, so what was worked out from it stays.
/// </remarks>
internal sealed class AnidbAiringDateCache
{
    private readonly AiringScheduleService _service;

    private readonly Lazy<IMetadataService> _metadataService;

    private readonly Lazy<IMetadataLinkingService> _linkingService;

    private readonly Lock _buildLock = new();

    private volatile IReadOnlyDictionary<int, AnidbAnimeAiringDates>? _snapshot;

    private volatile bool _isStale = true;

    private int _listening;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnidbAiringDateCache"/> class.
    /// </summary>
    /// <param name="service">The service whose airings are gathered, and which raises their changes.</param>
    /// <param name="metadataService">Raises the series and episode changes.</param>
    /// <param name="linkingService">Raises the link changes.</param>
    /// <exception cref="ArgumentNullException">Any of the parameters is <c>null</c>.</exception>
    public AnidbAiringDateCache(
        AiringScheduleService service,
        Lazy<IMetadataService> metadataService,
        Lazy<IMetadataLinkingService> linkingService
    )
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(linkingService);

        _service = service;
        _metadataService = metadataService;
        _linkingService = linkingService;
    }

    #region Reading

    /// <summary>
    /// The dates of an AniDB anime's stored airings.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The dates, or <c>null</c> when it has none.</returns>
    public AnidbAnimeAiringDates? Get(int animeID)
        => GetSnapshot().TryGetValue(animeID, out var dates) ? dates : null;

    /// <summary>
    /// The current snapshot, built again after a change.
    /// </summary>
    /// <returns>The dates, by AniDB anime ID.</returns>
    private IReadOnlyDictionary<int, AnidbAnimeAiringDates> GetSnapshot()
    {
        if (!_isStale && _snapshot is { } current)
            return current;

        lock (_buildLock)
        {
            Listen();
            if (!_isStale && _snapshot is { } snapshot)
                return snapshot;

            // Cleared before the rows are read, so a change made meanwhile is
            // picked up by the next read.
            _isStale = false;
            var built = _service.BuildAnidbAiringDates();
            if (_snapshot is { } previous)
            {
                foreach (var (animeID, dates) in built)
                {
                    if (previous.TryGetValue(animeID, out var kept) && kept.HasSameDates(dates))
                        built[animeID] = kept;
                }
            }

            return _snapshot = built;
        }
    }

    #endregion

    #region Invalidation

    /// <summary>
    /// Marks the cache stale.
    /// </summary>
    public void MarkStale()
        => _isStale = true;

    /// <summary>
    /// Starts listening to the changes that make the cache stale, once.
    /// </summary>
    private void Listen()
    {
        if (Interlocked.Exchange(ref _listening, 1) is 1)
            return;

        _service.AiringsUpdated += (_, _) => MarkStale();
        _service.ScheduleUpdated += (_, _) => MarkStale();
        _service.ProvidersUpdated += (_, _) => MarkStale();
        var metadataService = _metadataService.Value;
        metadataService.SeriesAdded += OnSeriesChanged;
        metadataService.SeriesUpdated += OnSeriesChanged;
        metadataService.SeriesRemoved += OnSeriesChanged;
        metadataService.EpisodeAdded += OnEpisodeChanged;
        metadataService.EpisodeUpdated += OnEpisodeChanged;
        metadataService.EpisodeRemoved += OnEpisodeChanged;
        _linkingService.Value.LinksChanged += (_, _) => MarkStale();
    }

    /// <summary>
    /// Marks the cache stale when a series is added, updated or removed.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The update.</param>
    private void OnSeriesChanged(object? sender, SeriesInfoUpdatedEventArgs eventArgs)
        => MarkStale();

    /// <summary>
    /// Marks the cache stale when an episode is added, updated or removed.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The update.</param>
    private void OnEpisodeChanged(object? sender, EpisodeInfoUpdatedEventArgs eventArgs)
        => MarkStale();

    #endregion
}
