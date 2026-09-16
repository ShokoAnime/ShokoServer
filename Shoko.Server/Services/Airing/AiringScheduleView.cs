using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Airing;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// The enriched view of one stored schedule. Only what the provider submitted
/// is stored, so the channel, the provider, the series, the season, the track
/// languages and the time zone are all resolved when they are first read,
/// which is what keeps a new alias, a relink or a priority change from leaving
/// stale enrichment behind.
/// </summary>
internal sealed class AiringScheduleView : IAiringSchedule
{
    private readonly AiringReadContext _context;

    private readonly AiringSchedule _row;

    private IReadOnlyList<IAiringTrack>? _tracks;

    private bool _providerResolved;

    private AiringScheduleProviderInfo? _provider;

    private bool _seriesResolved;

    private ISeries? _series;

    private bool _seasonResolved;

    private ISeason? _season;

    private bool _channelResolved;

    private IAiringChannel? _channel;

    private bool _timeZoneResolved;

    private TimeZoneInfo? _timeZone;

    /// <summary>
    /// The stored row behind this view, for the service's own writes and
    /// ownership checks.
    /// </summary>
    public AiringSchedule Row => _row;

    /// <summary>
    /// Whether the schedule has any track left after the disabled kinds are
    /// taken out. A schedule with none is hidden from reads.
    /// </summary>
    public bool HasVisibleTracks => Tracks.Count > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringScheduleView"/> class.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="row">The stored schedule.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="row"/> is <see langword="null"/>.</exception>
    public AiringScheduleView(AiringReadContext context, AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);

        _context = context;
        _row = row;
    }

    /// <inheritdoc/>
    public Guid ID => _row.ID;

    /// <inheritdoc/>
    public string Key => _row.Key;

    /// <inheritdoc/>
    public Guid ProviderID => _row.ProviderID;

    /// <inheritdoc/>
    public string ProviderName => _row.ProviderName;

    /// <inheritdoc/>
    public AiringScheduleProviderInfo? Provider
    {
        get
        {
            if (_providerResolved)
                return _provider;

            _providerResolved = true;
            return _provider = _context.GetProvider(_row.ProviderID);
        }
    }

    /// <inheritdoc/>
    public DataSource SeriesSource => _row.SeriesSource;

    /// <inheritdoc/>
    public string SeriesID => _row.SeriesID;

    /// <inheritdoc/>
    public ISeries? Series
    {
        get
        {
            if (_seriesResolved)
                return _series;

            _seriesResolved = true;
            return _series = _context.GetSeries(_row.SeriesSource, _row.SeriesID);
        }
    }

    /// <inheritdoc/>
    public string? SeasonID => string.IsNullOrEmpty(_row.SeasonID) ? null : _row.SeasonID;

    /// <inheritdoc/>
    public ISeason? Season
    {
        get
        {
            if (_seasonResolved)
                return _season;

            _seasonResolved = true;
            return _season = SeasonID is { } seasonID ? _context.GetSeason(_row.SeriesSource, seasonID) : null;
        }
    }

    /// <inheritdoc/>
    public IAiringChannel? Channel
    {
        get
        {
            if (_channelResolved)
                return _channel;

            _channelResolved = true;
            return _channel = _row.ChannelID is { } channelID ? _context.GetChannel(channelID) : null;
        }
    }

    /// <inheritdoc/>
    public string? Url => _row.Url;

    /// <inheritdoc/>
    public IReadOnlyList<IAiringTrack> Tracks
    {
        get
        {
            if (_tracks is not null)
                return _tracks;

            var visibleKinds = _context.GetVisibleKinds(_row.ProviderID);
            return _tracks = _row.Tracks
                .Where(track => visibleKinds.Contains(track.Kind))
                .Select(IAiringTrack (track) => new AiringTrack(track))
                .ToList();
        }
    }

    /// <inheritdoc/>
    public int? FirstEpisodeNumber => _row.FirstEpisodeNumber;

    /// <inheritdoc/>
    public int? LastEpisodeNumber => _row.LastEpisodeNumber;

    /// <inheritdoc/>
    public bool IsFinished => _row.IsFinished;

    /// <inheritdoc/>
    public string? TimeZoneID => _row.TimeZoneID;

    /// <inheritdoc/>
    public TimeZoneInfo? TimeZone
    {
        get
        {
            if (_timeZoneResolved)
                return _timeZone;

            _timeZoneResolved = true;
            // A stored id can stop resolving when a zone is renamed or dropped
            // from tzdata, and the caller should still see the id itself.
            return _timeZone = _row.TimeZoneID is { Length: > 0 } timeZoneID && AiringScheduleService.TryResolveTimeZone(timeZoneID, out var zone) ? zone : null;
        }
    }

    /// <inheritdoc/>
    public DateTime CreatedAt => _row.CreatedAt;

    /// <inheritdoc/>
    public DateTime LastUpdatedAt => _row.LastUpdatedAt;
}
