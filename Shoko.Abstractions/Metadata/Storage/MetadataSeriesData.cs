using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A series to store, whole: its own fields, its titles and overviews,
///   and every season and episode it has. Saving it replaces what was stored
///   for the series, so a season or episode left out is removed.
/// </summary>
public sealed record MetadataSeriesData
{
    /// <summary>
    ///   The series: its source, the <c>series</c> kind and the source's own
    ///   ID for it, e.g. <c>anilist://series/21</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The series' titles, in order. They are stored under the series'
    ///   source, whatever source each title names.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the series's default.
    ///   Without one, the series gets a synthesized default such as
    ///   <c>TMDB Series 46195</c>, never stored.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The series' overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   What kind of series it is.
    /// </summary>
    public AnimeType Type { get; init; } = AnimeType.Unknown;

    /// <summary>
    ///   When the series first aired, if fully or partially known.
    /// </summary>
    public PartialDateOnly? AirDate { get; init; }

    /// <summary>
    ///   When the series ended, if fully or partially known.
    /// </summary>
    public PartialDateOnly? EndDate { get; init; }

    /// <summary>
    ///   The source's user rating, on a scale of 1 to 10.
    /// </summary>
    public double Rating { get; init; }

    /// <summary>
    ///   How many votes the rating is made from.
    /// </summary>
    public int RatingVotes { get; init; }

    /// <summary>
    ///   Whether the series is for adults only.
    /// </summary>
    public bool Restricted { get; init; }

    /// <summary>
    ///   Where the series is in its release.
    /// </summary>
    public ReleaseStatus ReleaseStatus { get; init; } = ReleaseStatus.Unknown;

    /// <summary>
    ///   What the series was adapted from.
    /// </summary>
    public SourceMaterial SourceMaterial { get; init; } = SourceMaterial.Unknown;

    /// <summary>
    ///   The language the series was first made in, as a language code, when
    ///   the source says. At most 32 characters.
    /// </summary>
    public string? OriginalLanguageCode { get; init; }

    /// <summary>
    ///   The countries the series was made in, in order, as ISO 3166-1 codes
    ///   when the source gives them. A country given twice is kept once.
    /// </summary>
    public IReadOnlyList<string> ProductionCountries { get; init; } = [];

    /// <summary>
    ///   How popular the series is, on the source's own scale, when the
    ///   source measures it.
    /// </summary>
    public double? Popularity { get; init; }

    /// <summary>
    ///   How many of the source's users marked the series a favorite, when
    ///   the source counts them.
    /// </summary>
    public int? FavoriteCount { get; init; }

    /// <summary>
    ///   Links to the series elsewhere, such as its homepage.
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; init; } = [];

    /// <summary>
    ///   The IDs other sources gave the same series, as the source lists
    ///   them, e.g. <c>imdb://series/tt0000001</c>. The source of each may be one
    ///   nobody registered. An ID given twice is kept once.
    /// </summary>
    public IReadOnlyList<MetadataGuid> CrossSourceIDs { get; init; } = [];

    /// <summary>
    ///   The series' content ratings, in order, a country as often as it
    ///   is rated. A rating given twice for a country keeps its first place.
    /// </summary>
    public IReadOnlyList<MetadataContentRatingData> ContentRatings { get; init; } = [];

    /// <summary>
    ///   Every season of the series. Each must be on the series' source.
    /// </summary>
    public IReadOnlyList<MetadataSeasonData> Seasons { get; init; } = [];

    /// <summary>
    ///   Every episode of the series. Each must be on the series' source, and
    ///   a season it names must be one of <see cref="Seasons"/>.
    /// </summary>
    public IReadOnlyList<MetadataEpisodeData> Episodes { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the series's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the series has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}

/// <summary>
///   A season to store, as part of its series.
/// </summary>
public sealed record MetadataSeasonData
{
    /// <summary>
    ///   The season: its source, the <c>season</c> kind and the source's own
    ///   ID for it, e.g. <c>anilist://season/21-1</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The season's number in the series' own ordering.
    /// </summary>
    public required int SeasonNumber { get; init; }

    /// <summary>
    ///   The season's titles, in order, stored under the season's source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the season's default.
    ///   Without one, the season gets its generic name, such as
    ///   <c>Season 2</c>, synthesized in the user's languages. Generic names are
    ///   left out, so a season named only by one is better saved without.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The season's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the season's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the season has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}

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
