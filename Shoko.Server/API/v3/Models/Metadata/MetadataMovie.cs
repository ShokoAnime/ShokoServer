using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A movie of any metadata source.
/// </summary>
public class MetadataMovie : MetadataEntry
{
    #region Details

    /// <summary>
    /// When the movie was released, if known.
    /// </summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// Whether the movie is for adults only.
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// Whether the source marks the movie as a standalone video rather than a
    /// film proper.
    /// </summary>
    [Required]
    public bool IsVideo { get; init; }

    /// <summary>
    /// The original language, as a code, if the source says.
    /// </summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>
    /// The rating the source's users give the movie, on a scale of 1 to 10,
    /// or <c>null</c> when the source has none.
    /// </summary>
    public Rating? Rating { get; init; }

    /// <summary>
    /// The names of the movie's genres.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>
    /// The Shoko series linked to the movie.
    /// </summary>
    [Required]
    public IReadOnlyList<int> ShokoSeriesIDs { get; init; } = [];

    /// <summary>
    /// The Shoko episodes linked to the movie.
    /// </summary>
    [Required]
    public IReadOnlyList<int> ShokoEpisodeIDs { get; init; } = [];

    /// <summary>
    /// When the movie was last refreshed in full, or <c>null</c> when it
    /// never was.
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
    /// The links from AniDB to the movie, when asked for.
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
    /// The yearly seasons the movie came out in, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<SeasonWithYear>? YearlySeasons { get; init; }

    #endregion
}

/// <summary>
/// A collection of movies and series of any metadata source.
/// </summary>
public class MetadataCollection : MetadataEntry
{
    /// <summary>
    /// How many of the collection's movies are stored.
    /// </summary>
    [Required]
    public int MovieCount { get; init; }

    /// <summary>
    /// How many of the collection's series are stored.
    /// </summary>
    [Required]
    public int SeriesCount { get; init; }
}
