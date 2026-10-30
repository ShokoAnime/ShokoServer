using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   An episode to store, as part of its series.
/// </summary>
public sealed record MetadataEpisodeData
{
    /// <summary>
    ///   The episode: its source, the <c>episode</c> kind and the source's
    ///   own ID for it, e.g. <c>anilist://episode/88</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The season the episode sits in, or <c>null</c> for none. It must be
    ///   one of the series' <see cref="MetadataSeriesData.Seasons"/>.
    /// </summary>
    public MetadataGuid? SeasonID { get; init; }

    /// <summary>
    ///   The season's number, when the episode sits in one. Left out, it is
    ///   read from the season named by <see cref="SeasonID"/>.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    ///   The episode's number, within its season when it has one.
    /// </summary>
    public required int EpisodeNumber { get; init; }

    /// <summary>
    ///   What kind of episode it is.
    /// </summary>
    public EpisodeType Type { get; init; } = EpisodeType.Episode;

    /// <summary>
    ///   The season of the regular episode a special airs before. Honoured
    ///   only for an episode in season 0, with the numbers of the series' own
    ///   seasons and episodes. A target that does not exist is ignored.
    /// </summary>
    public int? AirsBeforeSeasonNumber { get; init; }

    /// <summary>
    ///   The number of the regular episode a special airs before, within
    ///   <see cref="AirsBeforeSeasonNumber"/>. Honoured only for an episode
    ///   in season 0. A target that does not exist is ignored.
    /// </summary>
    public int? AirsBeforeEpisodeNumber { get; init; }

    /// <summary>
    ///   The season a special airs after, once its last episode aired.
    ///   Honoured only for an episode in season 0, with the numbers of the
    ///   series' own seasons. A target that does not exist is ignored.
    /// </summary>
    public int? AirsAfterSeasonNumber { get; init; }

    /// <summary>
    ///   The source's user rating, on a scale of 1 to 10.
    /// </summary>
    public double Rating { get; init; }

    /// <summary>
    ///   How many votes the rating is made from.
    /// </summary>
    public int RatingVotes { get; init; }

    /// <summary>
    ///   How long the episode runs, or <see cref="TimeSpan.Zero"/> when not
    ///   known. Stored to the second.
    /// </summary>
    public TimeSpan Runtime { get; init; }

    /// <summary>
    ///   The day the episode aired. Left out when
    ///   <see cref="AirDateWithTime"/> is given, whose UTC day it then is.
    /// </summary>
    public DateOnly? AirDate { get; init; }

    /// <summary>
    ///   The precise day and time the episode aired, in UTC, when known.
    /// </summary>
    public DateTime? AirDateWithTime { get; init; }

    /// <summary>
    ///   Links to the episode elsewhere, such as its page on another site.
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; init; } = [];

    /// <summary>
    ///   The IDs other sources gave the same episode, as the source lists
    ///   them, e.g. <c>imdb://episode/tt0000001</c>. The source of each may be one
    ///   nobody registered. An ID given twice is kept once.
    /// </summary>
    public IReadOnlyList<MetadataGuid> CrossSourceIDs { get; init; } = [];

    /// <summary>
    ///   The episode's titles, in order, stored under the episode's source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the episode's
    ///   default. Without one, the episode gets its generic title, such as
    ///   <c>Episode 5</c>, synthesized in the user's languages. Generic titles are
    ///   left out, so an episode named only by one is better saved without.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The episode's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the episode's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the episode has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}
