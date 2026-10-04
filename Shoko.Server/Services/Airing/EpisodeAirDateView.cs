using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// A date-only entry: an AniDB episode known only by its air date, with no
/// airing behind it. It has no schedule, provider, channel or time, and is
/// never stored.
/// </summary>
internal sealed class EpisodeAirDateView : IEpisodeAiring
{
    private readonly IAnidbEpisode _anidbEpisode;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeAirDateView"/> class.
    /// </summary>
    /// <param name="anidbEpisode">The AniDB episode.</param>
    /// <param name="shokoEpisode">The shoko episode it belongs to, if any.</param>
    /// <param name="airDate">The AniDB air date, or the linked one standing in for AniDB's pre-1970 placeholder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="anidbEpisode"/> is <c>null</c>.</exception>
    public EpisodeAirDateView(IAnidbEpisode anidbEpisode, IShokoEpisode? shokoEpisode, DateOnly airDate)
    {
        ArgumentNullException.ThrowIfNull(anidbEpisode);

        _anidbEpisode = anidbEpisode;
        ShokoEpisode = shokoEpisode;
        AirDate = airDate;
        Key = $"date-only:{AiringScheduleUtility.GetDerivedAiringKey(anidbEpisode.Source, anidbEpisode.ID.ID)}";
    }

    /// <inheritdoc/>
    public Guid ID => AiringScheduleUtility.GetDateOnlyAiringID(Key);

    /// <inheritdoc/>
    public string Key { get; }

    /// <inheritdoc/>
    public IAiringSchedule? Schedule => null;

    /// <inheritdoc/>
    public Guid? ProviderID => null;

    /// <inheritdoc/>
    public string? ProviderName => null;

    /// <inheritdoc/>
    public bool IsDateOnly => true;

    /// <inheritdoc/>
    public DateOnly? AirDate { get; }

    /// <inheritdoc/>
    public MetadataGuid EpisodeID => _anidbEpisode.ID;

    /// <inheritdoc/>
    public IEpisode? Episode => _anidbEpisode;

    /// <inheritdoc/>
    public IAnidbEpisode? AnidbEpisode => _anidbEpisode;

    /// <inheritdoc/>
    public IShokoEpisode? ShokoEpisode { get; }

    /// <inheritdoc/>
    public IAiringChannel? Channel => null;

    /// <inheritdoc/>
    public string? Url => null;

    /// <inheritdoc/>
    public IReadOnlyList<IAiringTrack> Tracks => [];

    /// <inheritdoc/>
    public DateTime? AiredAt => null;

    /// <inheritdoc/>
    public DateTime? OriginalAiredAt => null;

    /// <inheritdoc/>
    public bool IsDelayed => false;

    /// <inheritdoc/>
    public bool IsEstimated => false;

    /// <summary>
    /// Always <c>true</c>: a date-only entry is only made for an episode with
    /// no airing at all, so it is the only one its episode has.
    /// </summary>
    public bool IsPreferred => true;

    /// <inheritdoc/>
    public EpisodeAiringKind Kind => EpisodeAiringKind.Normal;

    /// <inheritdoc/>
    public TimeSpan? OffsetFromOriginal => null;

    /// <inheritdoc/>
    public Guid? LinkID => null;

    /// <inheritdoc/>
    public DateTime CreatedAt => _anidbEpisode.CreatedAt;

    /// <inheritdoc/>
    public DateTime LastUpdatedAt => _anidbEpisode.LastUpdatedAt;

    /// <summary>
    /// The instant a date-only entry is placed at: the start of its day in the
    /// given offset.
    /// </summary>
    /// <param name="offset">The offset the day is read in.</param>
    /// <returns>The start of the day, in UTC.</returns>
    public DateTime GetDayStart(TimeSpan offset)
        => new DateTimeOffset(AirDate!.Value.ToDateTime(TimeOnly.MinValue), offset).UtcDateTime;
}
