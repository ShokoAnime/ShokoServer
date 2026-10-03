using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

using Resource = Shoko.Server.API.v3.Models.Common.Resource;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A series of any metadata source.
/// </summary>
public class MetadataSeries : MetadataEntry
{
    #region Details

    /// <summary>
    /// What kind of release the series is.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public AnimeType AnimeType { get; init; }

    /// <summary>
    /// Whether the series is still airing.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public ReleaseStatus ReleaseStatus { get; init; }

    /// <summary>
    /// What the series was adapted from.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public SourceMaterial SourceMaterial { get; init; }

    /// <summary>
    /// The original language, as a code, if the source says.
    /// </summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>
    /// Whether the series is for adults only.
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// When the series first aired, if known.
    /// </summary>
    public PartialDateOnly? AirDate { get; init; }

    /// <summary>
    /// When the series last aired, if known.
    /// </summary>
    public PartialDateOnly? EndDate { get; init; }

    /// <summary>
    /// The rating the source's users give the series, on a scale of 1 to 10,
    /// or <c>null</c> when the source has none.
    /// </summary>
    public Rating? Rating { get; init; }

    /// <summary>
    /// How popular the series is on the source, if it says.
    /// </summary>
    public double? Popularity { get; init; }

    /// <summary>
    /// How many of the source's users marked the series as a favourite, if it
    /// says.
    /// </summary>
    public int? FavoriteCount { get; init; }

    /// <summary>
    /// How many episodes of each type the series has.
    /// </summary>
    [Required]
    public EpisodeCounts EpisodeCounts { get; init; } = new();

    /// <summary>
    /// How many seasons the series has, specials not counted.
    /// </summary>
    [Required]
    public int SeasonCount { get; init; }

    /// <summary>
    /// The names of the series' genres.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>
    /// The Shoko series linked to the series.
    /// </summary>
    [Required]
    public IReadOnlyList<int> ShokoSeriesIDs { get; init; } = [];

    /// <summary>
    /// When the series was first stored locally, in UTC.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the series was last updated locally, in UTC.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// When the series was last refreshed in full, in UTC, or <c>null</c> when
    /// it never was.
    /// </summary>
    public DateTime? LastRefreshedAt { get; init; }

    #endregion

    #region Include Blocks

    /// <summary>
    /// The tags, genres included, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataTag>? Tags { get; init; }

    /// <summary>
    /// The studios, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataStudio>? Studios { get; init; }

    /// <summary>
    /// The networks, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataNetwork>? Networks { get; init; }

    /// <summary>
    /// The cast, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataRole>? Cast { get; init; }

    /// <summary>
    /// The crew, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataRole>? Crew { get; init; }

    /// <summary>
    /// The links from AniDB anime to the series, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataCrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// The external resources, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Resource>? Resources { get; init; }

    /// <summary>
    /// The content ratings, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<ContentRating>? ContentRatings { get; init; }

    /// <summary>
    /// The yearly seasons the series aired in, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<SeasonWithYear>? YearlySeasons { get; init; }

    #endregion
}

/// <summary>
/// A season of a series of any metadata source.
/// </summary>
public class MetadataSeason : MetadataEntry
{
    /// <summary>
    /// The source's ID of the series the season belongs to.
    /// </summary>
    [Required]
    public string SeriesID { get; init; } = string.Empty;

    /// <summary>
    /// The season's number, <c>0</c> for specials.
    /// </summary>
    [Required]
    public int SeasonNumber { get; init; }

    /// <summary>
    /// How many episodes the season has.
    /// </summary>
    [Required]
    public int EpisodeCount { get; init; }

    /// <summary>
    /// When the season was first stored locally, in UTC.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the season was last updated locally, in UTC.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// When the season's series was last refreshed in full, in UTC, or <c>null</c> when
    /// it never was.
    /// </summary>
    public DateTime? LastRefreshedAt { get; init; }

    /// <summary>
    /// The cast, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataRole>? Cast { get; init; }

    /// <summary>
    /// The crew, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataRole>? Crew { get; init; }

    /// <summary>
    /// The yearly seasons the season aired in, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<SeasonWithYear>? YearlySeasons { get; init; }
}

/// <summary>
/// An episode of a series of any metadata source.
/// </summary>
public class MetadataEpisode : MetadataEntry
{
    /// <summary>
    /// The source's ID of the series the episode belongs to.
    /// </summary>
    [Required]
    public string SeriesID { get; init; } = string.Empty;

    /// <summary>
    /// The source's ID of the season the episode belongs to, if the source
    /// has seasons.
    /// </summary>
    public string? SeasonID { get; init; }

    /// <summary>
    /// What type of episode it is.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public EpisodeType EpisodeType { get; init; }

    /// <summary>
    /// The episode's number, within its season when the source has seasons.
    /// </summary>
    [Required]
    public int EpisodeNumber { get; init; }

    /// <summary>
    /// The number of the season the episode belongs to, if the source has
    /// seasons.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    /// How long the episode runs.
    /// </summary>
    [Required]
    public TimeSpan Runtime { get; init; }

    /// <summary>
    /// The day the episode first aired, if known.
    /// </summary>
    public DateOnly? AirDate { get; init; }

    /// <summary>
    /// When the episode first aired, with the time of day, if known.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// The rating the source's users give the episode, on a scale of 1 to 10,
    /// or <c>null</c> when the source has none.
    /// </summary>
    public Rating? Rating { get; init; }

    /// <summary>
    /// The Shoko episodes linked to the episode.
    /// </summary>
    [Required]
    public IReadOnlyList<int> ShokoEpisodeIDs { get; init; } = [];

    /// <summary>
    /// When the episode was first stored locally, in UTC.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the episode was last updated locally, in UTC.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// When the episode's series was last refreshed in full, in UTC, or <c>null</c> when
    /// it never was.
    /// </summary>
    public DateTime? LastRefreshedAt { get; init; }

    /// <summary>
    /// The cast, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataRole>? Cast { get; init; }

    /// <summary>
    /// The crew, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataRole>? Crew { get; init; }

    /// <summary>
    /// The links from AniDB episodes to the episode, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<MetadataCrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// The external resources, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Resource>? Resources { get; init; }
}
