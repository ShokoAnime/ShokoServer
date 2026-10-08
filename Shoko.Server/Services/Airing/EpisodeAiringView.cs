using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services.Airing;

/// <summary>
/// The enriched view of one airing, stored or estimated, as it was resolved
/// for one episode. The same stored row comes back once per episode it is
/// linked to, each time with that episode's own AniDB and shoko views
/// attached, which is why this is a view rather than the row itself.
/// </summary>
internal sealed class EpisodeAiringView : IEpisodeAiring
{
    private readonly AiringReadContext _context;

    private readonly AiringScheduleView _schedule;

    private readonly EpisodeAiring? _row;

    private readonly IEpisode? _resolvedFor;

    private readonly EpisodeAiringKind _providerKind;

    private readonly MetadataSource? _pinnedSource;

    private readonly string? _pinnedID;

    private bool _episodeResolved;

    private IEpisode? _episode;

    private bool _episodeViewsResolved;

    private IAnidbEpisode? _anidbEpisode;

    private IShokoEpisode? _shokoEpisode;

    private (int AnimeID, int EpisodeNumber)? _anidbPosition;

    private bool _linkResolved;

    private Guid? _linkID;

    private bool _offsetResolved;

    private TimeSpan? _offsetFromOriginal;

    private EpisodeAiringKind? _kind;

    private bool _durationResolved;

    private TimeSpan? _duration;

    private bool _endsAtResolved;

    private DateTime? _endsAt;

    /// <summary>
    /// The stored row behind this view, or <c>null</c> when the view
    /// is an estimate.
    /// </summary>
    public EpisodeAiring? Row => _row;

    /// <summary>
    /// The schedule view this airing belongs to, shared with every other airing
    /// of the schedule in the same read.
    /// </summary>
    public AiringScheduleView ScheduleView => _schedule;

    /// <summary>
    /// The episode the read this view belongs to ran for, or
    /// <c>null</c> when the read named none and the view stands for
    /// the airing's own episode. A read that resolved one is what says two
    /// views of different stored episodes are the same episode after the links
    /// were followed.
    /// </summary>
    public IEpisode? ResolvedFor => _resolvedFor;

    /// <summary>
    /// The key the read gathered this view under, or <c>null</c> when it
    /// gathered it under none, so <see cref="AiringEpisodeKey.For"/> works it
    /// out from the view itself.
    /// </summary>
    public AiringEpisodeKey? TargetKey { get; }

    /// <summary>
    /// What a read gathering this airing's other showings asks for: the
    /// episode it was read for, else its own, under its key.
    /// </summary>
    public AiringReadTarget Target => new(_resolvedFor ?? Episode, AiringEpisodeKey.For(this));

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeAiringView"/> class
    /// over a stored airing.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="schedule">The schedule the airing is on.</param>
    /// <param name="row">The stored airing.</param>
    /// <param name="resolvedFor">The episode the read ran for, when it isn't the airing's own.</param>
    /// <param name="targetKey">The key the read gathered the airing under, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="schedule"/> or <paramref name="row"/> is <c>null</c>.</exception>
    public EpisodeAiringView(
        AiringReadContext context,
        AiringScheduleView schedule,
        EpisodeAiring row,
        IEpisode? resolvedFor = null,
        AiringEpisodeKey? targetKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(row);

        _context = context;
        _schedule = schedule;
        _row = row;
        _resolvedFor = resolvedFor;
        TargetKey = targetKey;
        Key = row.Key;
        SequenceNumber = row.SequenceNumber;
        if (row.IsPinned)
        {
            _pinnedSource = row.EpisodeSource;
            _pinnedID = row.EpisodeID;
        }

        AiredAt = row.AiredAt;
        OriginalAiredAt = row.OriginalAiredAt;
        IsDelayed = row.IsDelayed;
        _providerKind = row.Kind;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeAiringView"/> class
    /// over an estimate, which is never stored and belongs to its schedule just
    /// as a real airing does.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="schedule">The schedule the estimate belongs to.</param>
    /// <param name="episodeSource">The source of the episode being estimated.</param>
    /// <param name="episodeID">The ID of the episode within its source.</param>
    /// <param name="sequenceNumber">The episode's place on the schedule's line.</param>
    /// <param name="key">The key the estimate's ID is derived from.</param>
    /// <param name="airedAt">The estimated air time, or <c>null</c> while the schedule is on hiatus.</param>
    /// <param name="originalAiredAt">The slot the episode would have had, when it differs from <paramref name="airedAt"/>.</param>
    /// <param name="resolvedFor">The episode the read ran for.</param>
    /// <param name="targetKey">The key the read gathered the estimate under, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="schedule"/> or <paramref name="key"/> is <c>null</c>.</exception>
    public EpisodeAiringView(
        AiringReadContext context,
        AiringScheduleView schedule,
        MetadataSource episodeSource,
        string episodeID,
        int sequenceNumber,
        string key,
        DateTime? airedAt,
        DateTime? originalAiredAt,
        IEpisode? resolvedFor = null,
        AiringEpisodeKey? targetKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(key);

        _context = context;
        _schedule = schedule;
        _resolvedFor = resolvedFor;
        TargetKey = targetKey;
        Key = key;
        SequenceNumber = sequenceNumber;
        _pinnedSource = episodeSource;
        _pinnedID = episodeID;
        AiredAt = airedAt;
        OriginalAiredAt = originalAiredAt;
        IsDelayed = false;
        IsEstimated = true;
        _providerKind = EpisodeAiringKind.Normal;
    }

    /// <inheritdoc/>
    public Guid ID => AiringScheduleUtility.GetEpisodeAiringID(_schedule.ID, Key);

    /// <inheritdoc/>
    public string Key { get; }

    /// <inheritdoc/>
    public IAiringSchedule Schedule => _schedule;

    /// <summary>
    ///   The ID of the provider that owns the airing's schedule.
    /// </summary>
    public Guid ProviderID => _schedule.ProviderID;

    /// <inheritdoc/>
    Guid? IEpisodeAiring.ProviderID => _schedule.ProviderID;

    /// <inheritdoc/>
    public string ProviderName => _schedule.ProviderName;

    /// <inheritdoc/>
    public bool IsDateOnly => false;

    /// <inheritdoc/>
    public DateOnly? AirDate => null;

    /// <inheritdoc/>
    public int? SequenceNumber { get; }

    /// <inheritdoc/>
    public int? EpisodeNumber => SequenceNumber is { } sequenceNumber ? _schedule.Row.FirstEpisodeNumber + sequenceNumber - 1 : null;

    /// <inheritdoc/>
    public MetadataGuid? EpisodeID
        => _pinnedSource is { } source && _pinnedID is { } id
            ? new(source, MetadataEntityType.Episode, id)
            : Episode?.ID;

    /// <inheritdoc/>
    public IEpisode? Episode
    {
        get
        {
            if (_episodeResolved)
                return _episode;

            _episodeResolved = true;
            if (_pinnedSource is { } source && _pinnedID is { } id)
                return _episode = _context.GetEpisode(source, id);

            return _episode = EpisodeNumber is { } episodeNumber ? _context.GetScheduleEpisodeByNumber(_schedule.Row, episodeNumber) : null;
        }
    }

    /// <inheritdoc/>
    public IAnidbEpisode? AnidbEpisode
    {
        get
        {
            ResolveEpisodeViews();
            return _anidbEpisode;
        }
    }

    /// <inheritdoc/>
    public IShokoEpisode? ShokoEpisode
    {
        get
        {
            ResolveEpisodeViews();
            return _shokoEpisode;
        }
    }

    /// <inheritdoc/>
    public int? AnidbAnimeID
    {
        get
        {
            ResolveEpisodeViews();
            return _anidbEpisode?.AnidbAnimeID ?? _anidbPosition?.AnimeID;
        }
    }

    /// <inheritdoc/>
    public IAnidbAnime? AnidbAnime => AnidbAnimeID is { } anidbAnimeID ? _context.GetAnidbAnime(anidbAnimeID) : null;

    /// <inheritdoc/>
    public int? AnidbEpisodeNumber
    {
        get
        {
            ResolveEpisodeViews();
            if (_anidbEpisode is { } anidbEpisode)
                return ((IEpisode)anidbEpisode).Type is EpisodeType.Episode ? anidbEpisode.EpisodeNumber : null;

            return _anidbPosition?.EpisodeNumber;
        }
    }

    /// <inheritdoc/>
    public IShokoSeries? ShokoSeries
        => ShokoEpisode?.Series ?? (AnidbAnimeID is { } anidbAnimeID ? _context.GetShokoSeriesByAnimeID(anidbAnimeID) : null);

    /// <inheritdoc/>
    public IAiringChannel? Channel => _schedule.Channel;

    /// <inheritdoc/>
    public string? Url => _row?.Url ?? _schedule.Url;

    /// <inheritdoc/>
    public IReadOnlyList<IAiringTrack> Tracks => _schedule.Tracks;

    /// <inheritdoc/>
    public DateTime? AiredAt { get; }

    /// <inheritdoc/>
    public DateTime? OriginalAiredAt { get; }

    /// <inheritdoc/>
    public TimeSpan? Duration
    {
        get
        {
            if (_durationResolved)
                return _duration;

            _durationResolved = true;
            return _duration = AnidbEpisode is { } anidbEpisode
                ? _context.GetEpisodeDuration(anidbEpisode)
                : AnidbAnimeID is { } anidbAnimeID ? _context.GetUsualEpisodeDuration(anidbAnimeID) : null;
        }
    }

    /// <inheritdoc/>
    public DateTime? EndsAt
    {
        get
        {
            if (_endsAtResolved)
                return _endsAt;

            _endsAtResolved = true;
            if (AiredAt is not { } start)
                return _endsAt = null;

            var channel = Channel;
            var nextSlot = channel is { Type: AiringChannelType.Television } ? _context.GetNextChannelSlot(channel.ChannelID, start) : null;
            return _endsAt = AiringScheduleUtility.GetAiringEnd(start, Duration, channel?.Type, nextSlot);
        }
    }

    /// <inheritdoc/>
    public bool IsDelayed { get; }

    /// <inheritdoc/>
    public bool IsEstimated { get; }

    /// <summary>
    /// Whether this is the airing of its episode that a preferred-only read
    /// with the same filters and window would keep. The list reads set it
    /// once they have ordered and windowed the episode's airings.
    /// </summary>
    public bool IsPreferred { get; set; }

    /// <inheritdoc/>
    public EpisodeAiringKind Kind => _kind ??= _context.GetEffectiveKind(_schedule.Row, _providerKind);

    /// <inheritdoc/>
    public TimeSpan? OffsetFromOriginal
    {
        get
        {
            if (_offsetResolved)
                return _offsetFromOriginal;

            _offsetResolved = true;
            if (AiredAt is not { } airedAt)
                return _offsetFromOriginal = null;
            if (_context.GetFirstOriginalAiringAt(Target) is not { } firstOriginal)
                return _offsetFromOriginal = null;
            // This airing is the anchor itself, so there is nothing to offset from.
            if (!IsEstimated && airedAt == firstOriginal)
                return _offsetFromOriginal = null;

            return _offsetFromOriginal = airedAt - firstOriginal;
        }
    }

    /// <inheritdoc/>
    public Guid? LinkID
    {
        get
        {
            if (_linkResolved)
                return _linkID;

            _linkResolved = true;
            if (_row?.LinkedToID is not { } linkedToID)
                return _linkID = null;

            return _linkID = RepoFactory.EpisodeAiring.GetByID(linkedToID) is { } head
                ? AiringScheduleUtility.GetEpisodeAiringID(_schedule.ID, head.Key)
                : null;
        }
    }

    /// <inheritdoc/>
    public DateTime CreatedAt => _row?.CreatedAt ?? _schedule.LastUpdatedAt;

    /// <inheritdoc/>
    public DateTime LastUpdatedAt => _row?.LastUpdatedAt ?? _schedule.LastUpdatedAt;

    /// <summary>
    /// Resolve the AniDB and shoko views of the episode this airing was read
    /// for, falling back to the airing's own episode when the read didn't name
    /// one, and to the AniDB episode its place on the line stands for when
    /// neither leads to AniDB.
    /// </summary>
    private void ResolveEpisodeViews()
    {
        if (_episodeViewsResolved)
            return;

        _episodeViewsResolved = true;
        (_anidbEpisode, _shokoEpisode) = _context.GetEpisodeViews(_resolvedFor ?? Episode);
        if (_anidbEpisode is not null || _shokoEpisode is not null || GetLineNumber() is not { } lineNumber)
            return;

        _anidbPosition = _context.GetAnidbPosition(_schedule.Row, lineNumber);
        if (_anidbPosition is { } position && _context.GetAnidbEpisode(position.AnimeID, position.EpisodeNumber) is { } anidbEpisode)
            (_anidbEpisode, _shokoEpisode) = _context.GetEpisodeViews(anidbEpisode);
    }

    /// <summary>
    /// The airing's number on its schedule's line: its sequence number's, else
    /// the number of the regular episode it is pinned to.
    /// </summary>
    /// <returns>The number, or <c>null</c> when it has none.</returns>
    private int? GetLineNumber()
        => EpisodeNumber ?? (Episode is { Type: EpisodeType.Episode } episode ? episode.EpisodeNumber : null);
}
