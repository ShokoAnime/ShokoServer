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

#nullable enable
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

    private bool _episodeResolved;

    private IEpisode? _episode;

    private bool _episodeViewsResolved;

    private IAnidbEpisode? _anidbEpisode;

    private IShokoEpisode? _shokoEpisode;

    private bool _linkResolved;

    private Guid? _linkID;

    private bool _offsetResolved;

    private TimeSpan? _offsetFromOriginal;

    /// <summary>
    /// The stored row behind this view, or <see langword="null"/> when the view
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
    /// <see langword="null"/> when the read named none and the view stands for
    /// the airing's own episode. A read that resolved one is what says two
    /// views of different stored episodes are the same episode after the links
    /// were followed.
    /// </summary>
    public IEpisode? ResolvedFor => _resolvedFor;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeAiringView"/> class
    /// over a stored airing.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="schedule">The schedule the airing is on.</param>
    /// <param name="row">The stored airing.</param>
    /// <param name="resolvedFor">The episode the read ran for, when it isn't the airing's own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="schedule"/> or <paramref name="row"/> is <see langword="null"/>.</exception>
    public EpisodeAiringView(AiringReadContext context, AiringScheduleView schedule, EpisodeAiring row, IEpisode? resolvedFor = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(row);

        _context = context;
        _schedule = schedule;
        _row = row;
        _resolvedFor = resolvedFor;
        Key = row.Key;
        EpisodeSource = row.EpisodeSource;
        EpisodeID = row.EpisodeID;
        AiredAt = row.AiredAt;
        OriginalAiredAt = row.OriginalAiredAt;
        IsDelayed = row.IsDelayed;
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
    /// <param name="key">The key the estimate's ID is derived from.</param>
    /// <param name="airedAt">The estimated air time, or <see langword="null"/> while the schedule is on hiatus.</param>
    /// <param name="originalAiredAt">The slot the episode would have had, when it differs from <paramref name="airedAt"/>.</param>
    /// <param name="resolvedFor">The episode the read ran for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="schedule"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    public EpisodeAiringView(
        AiringReadContext context,
        AiringScheduleView schedule,
        DataSource episodeSource,
        string episodeID,
        string key,
        DateTime? airedAt,
        DateTime? originalAiredAt,
        IEpisode? resolvedFor = null
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(key);

        _context = context;
        _schedule = schedule;
        _resolvedFor = resolvedFor;
        Key = key;
        EpisodeSource = episodeSource;
        EpisodeID = episodeID;
        AiredAt = airedAt;
        OriginalAiredAt = originalAiredAt;
        IsDelayed = false;
        IsEstimated = true;
    }

    /// <inheritdoc/>
    public Guid ID => AiringScheduleUtility.GetEpisodeAiringID(_schedule.ID, Key);

    /// <inheritdoc/>
    public string Key { get; }

    /// <inheritdoc/>
    public IAiringSchedule Schedule => _schedule;

    /// <inheritdoc/>
    public Guid ProviderID => _schedule.ProviderID;

    /// <inheritdoc/>
    public string ProviderName => _schedule.ProviderName;

    /// <inheritdoc/>
    public DataSource EpisodeSource { get; }

    /// <inheritdoc/>
    public string EpisodeID { get; }

    /// <inheritdoc/>
    public IEpisode? Episode
    {
        get
        {
            if (_episodeResolved)
                return _episode;

            _episodeResolved = true;
            return _episode = _context.GetEpisode(EpisodeSource, EpisodeID);
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
    public bool IsDelayed { get; }

    /// <inheritdoc/>
    public bool IsEstimated { get; }

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
            if (_context.GetFirstOriginalAiringAt(EpisodeSource, EpisodeID) is not { } firstOriginal)
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
    /// one.
    /// </summary>
    private void ResolveEpisodeViews()
    {
        if (_episodeViewsResolved)
            return;

        _episodeViewsResolved = true;
        (_anidbEpisode, _shokoEpisode) = _context.GetEpisodeViews(_resolvedFor ?? Episode);
    }
}
