using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// One episode on one airing schedule: a time, the slot it was first scheduled
/// for, the delay state the service inferred, and an optional link to the other
/// airings of the same slot.
/// </summary>
/// <remarks>
/// The default shape is slim on purpose: a calendar week is many episodes of
/// few series, so titles and images are opt-in through the <c>include</c>
/// query. An estimate is the same shape as a real airing, told apart by
/// <see cref="IsEstimated"/>.
/// </remarks>
public class EpisodeAiring
{
    /// <summary>
    /// The ID of the airing. Estimates get one too, derived the same way, so it
    /// is stable for as long as the estimate is.
    /// </summary>
    [Required]
    public Guid ID { get; init; }

    /// <summary>
    /// The ID of the schedule the airing is on.
    /// </summary>
    [Required]
    public Guid ScheduleID { get; init; }

    /// <summary>
    /// When the episode airs, in UTC, or <c>null</c> when the airing has no
    /// current slot.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// The slot the episode was first scheduled for, in UTC, or <c>null</c>
    /// while the slot has not moved. Draw a "no episode" marker here when it
    /// falls in the range and <see cref="AiredAt"/> does not.
    /// </summary>
    public DateTime? OriginalAiredAt { get; init; }

    /// <summary>
    /// Whether this airing's own slot was postponed on this channel. Only the
    /// airing that caused a delay is flagged; the ones that merely shifted
    /// behind it are not.
    /// </summary>
    [Required]
    public bool IsDelayed { get; init; }

    /// <summary>
    /// Whether the airing was computed from the schedule's other airings rather
    /// than reported by the provider.
    /// </summary>
    [Required]
    public bool IsEstimated { get; init; }

    /// <summary>
    /// This airing's time minus the episode's earliest known real
    /// <see cref="AiringKind.Original"/> airing, which a client can label as a
    /// simulcast or a lag. <c>null</c> when this is that airing, or when there
    /// is none. A negative offset is valid.
    /// </summary>
    public TimeSpan? OffsetFromOriginal { get; init; }

    /// <summary>
    /// The ID of the link head, shared by every airing of the same slot, or
    /// <c>null</c> when the airing is not linked. Linked airings are adjacent
    /// in a list, so a client can render one card per link.
    /// </summary>
    public Guid? LinkID { get; init; }

    /// <summary>
    /// The airing's own page, else the schedule's, or <c>null</c> when neither
    /// is known.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// The provider that owns the airing.
    /// </summary>
    [Required]
    public AiringSource Source { get; init; }

    /// <summary>
    /// The channel the episode airs on, or <c>null</c> when the schedule names
    /// none.
    /// </summary>
    public AiringChannelReference? Channel { get; init; }

    /// <summary>
    /// The time zone the schedule's source reports its times in, or <c>null</c>
    /// when it has none.
    /// </summary>
    public AiringTimeZone? TimeZone { get; init; }

    /// <summary>
    /// What the schedule releases, repeated here so a row can be labelled
    /// without fetching the schedule.
    /// </summary>
    [Required]
    public IReadOnlyList<AiringTrack> Tracks { get; init; }

    /// <summary>
    /// All IDs that may be useful for navigating away from a calendar.
    /// </summary>
    [Required]
    public AiringIDs IDs { get; init; }

    /// <summary>
    /// How many videos are in the collection for the episode. <c>0</c> when
    /// none are, which is also what an airing with no resolvable episode
    /// reports. Use it to mark a calendar row as already held.
    /// </summary>
    [Required]
    public int VideoCount { get; init; }

    /// <summary>
    /// The type of the episode, or <c>null</c> when no episode could be
    /// resolved for the airing.
    /// </summary>
    public EpisodeType? Type { get; init; }

    /// <summary>
    /// The number of the episode within its type, or <c>null</c> when no
    /// episode could be resolved for the airing.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// The title of the episode. Only set when
    /// <see cref="AiringDataToInclude.EpisodeTitle"/> is asked for.
    /// </summary>
    public string? EpisodeTitle { get; init; }

    /// <summary>
    /// The series the episode belongs to. Only set when
    /// <see cref="AiringDataToInclude.Series"/> is asked for.
    /// </summary>
    public AiringSeries? Series { get; init; }

    /// <summary>
    /// The series' primary image. Only set when
    /// <see cref="AiringDataToInclude.Poster"/> is asked for and one exists.
    /// </summary>
    public Image? Poster { get; init; }

    /// <summary>
    /// The episode's backdrop image, falling back to the series' when the
    /// episode has none. Only set when
    /// <see cref="AiringDataToInclude.Thumbnail"/> is asked for and one exists.
    /// </summary>
    public Image? Thumbnail { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeAiring"/> class.
    /// </summary>
    /// <param name="airing">The airing, as the airing schedule service resolved it for one episode.</param>
    /// <param name="series">The series to display, when it was asked for.</param>
    /// <param name="episodeTitle">The episode title to display, when it was asked for.</param>
    /// <param name="poster">The series poster, when it was asked for.</param>
    /// <param name="thumbnail">The episode thumbnail, when it was asked for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="airing"/> is <c>null</c>.</exception>
    public EpisodeAiring(IEpisodeAiring airing, AiringSeries? series = null, string? episodeTitle = null, Image? poster = null, Image? thumbnail = null)
    {
        ArgumentNullException.ThrowIfNull(airing);

        var episode = airing.AnidbEpisode ?? airing.ShokoEpisode?.AnidbEpisode ?? airing.Episode;
        ID = airing.ID;
        ScheduleID = airing.Schedule.ID;
        AiredAt = airing.AiredAt.ToUtc();
        OriginalAiredAt = airing.OriginalAiredAt.ToUtc();
        IsDelayed = airing.IsDelayed;
        IsEstimated = airing.IsEstimated;
        OffsetFromOriginal = airing.OffsetFromOriginal;
        LinkID = airing.LinkID;
        Url = airing.Url;
        Source = new(airing.ProviderID, airing.ProviderName);
        Channel = airing.Channel is { } channel ? new(channel) : null;
        TimeZone = airing.Schedule.TimeZoneID is { } timeZoneID ? new(timeZoneID, airing.Schedule.TimeZone) : null;
        Tracks = [.. airing.Tracks.Select(track => new AiringTrack(track))];
        IDs = new()
        {
            ShokoEpisode = airing.ShokoEpisode?.ID,
            AnidbEpisode = airing.AnidbEpisode?.ID,
            ShokoSeries = airing.ShokoEpisode?.Series?.ID,
            AnidbAnime = airing.AnidbEpisode?.SeriesID,
        };
        VideoCount = episode?.Videos.Count ?? 0;
        Type = episode?.Type;
        Number = episode?.EpisodeNumber;
        EpisodeTitle = episodeTitle;
        Series = series;
        Poster = poster;
        Thumbnail = thumbnail;
    }
}

/// <summary>
/// The IDs of the entities an airing belongs to. Every one of them is
/// <c>null</c> when that entity could not be resolved, which is never fatal:
/// the airing itself is always returned.
/// </summary>
public class AiringIDs
{
    /// <summary>
    /// The ID of the shoko episode the airing was resolved for.
    /// </summary>
    public int? ShokoEpisode { get; init; }

    /// <summary>
    /// The ID of the AniDB episode the airing was resolved for.
    /// </summary>
    public int? AnidbEpisode { get; init; }

    /// <summary>
    /// The ID of the shoko series the episode belongs to.
    /// </summary>
    public int? ShokoSeries { get; init; }

    /// <summary>
    /// The ID of the AniDB anime the episode belongs to.
    /// </summary>
    public int? AnidbAnime { get; init; }
}
