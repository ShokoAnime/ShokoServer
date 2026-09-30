using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A link from an AniDB anime or episode to an entry of any metadata source.
/// </summary>
public class MetadataCrossReference
{
    /// <summary>
    /// The source the link points at.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// The level the link is made at: a series, a season, an episode or a
    /// movie.
    /// </summary>
    [Required]
    public MetadataEntityType EntityType { get; init; } = null!;

    /// <summary>
    /// The AniDB anime the link is made from.
    /// </summary>
    [Required]
    public int AnidbAnimeID { get; init; }

    /// <summary>
    /// The AniDB episode an episode or movie link is made from.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    /// The source's ID of the linked entry, or <c>null</c> when the AniDB
    /// entry is deliberately linked to nothing.
    /// </summary>
    public string? ID { get; init; }

    /// <summary>
    /// The source's ID of the series a season or episode link points into, if
    /// known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ParentID { get; init; }

    /// <summary>
    /// The source's ID of the season an episode link points into, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? SeasonID { get; init; }

    /// <summary>
    /// The number of the season a season or episode link points into, if
    /// known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? SeasonNumber { get; init; }

    /// <summary>
    /// The number of the episode an episode link points at, if known.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// Which link this is, when more than one starts at the same AniDB entry,
    /// counting from <c>0</c>.
    /// </summary>
    [Required]
    public int Index { get; init; }

    /// <summary>
    /// How the link was arrived at.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public MatchRating MatchRating { get; init; }

    /// <summary>
    /// The provider that wrote the link, where one did.
    /// </summary>
    public Guid? WrittenBy { get; init; }
}

/// <summary>
/// Whether a source may link a Shoko series on its own.
/// </summary>
public class MetadataAutoLinkingState
{
    /// <summary>
    /// The source.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// Whether the source is kept from linking the series on its own, as it
    /// is once a person unlinks it.
    /// </summary>
    [Required]
    public bool Disabled { get; init; }
}
