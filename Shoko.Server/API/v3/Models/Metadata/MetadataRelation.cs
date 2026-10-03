using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// How a source relates one of its entries to another.
/// </summary>
public class MetadataRelation
{
    /// <summary>
    /// The source's ID of the related entry.
    /// </summary>
    [Required]
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// What kind of entry the related entry is.
    /// </summary>
    [Required]
    public MetadataEntityType Type { get; init; } = null!;

    /// <summary>
    /// The related entry's title, if it is stored.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// How the related entry relates to the entry asked about.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public RelationType RelationType { get; init; }

    /// <summary>
    /// The source that gave the relation.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// Whether the relation was checked by a person.
    /// </summary>
    [Required]
    public bool Verified { get; init; }
}

/// <summary>
/// An entry a source suggests beside one of its own, such as something
/// similar or recommended.
/// </summary>
public class MetadataSuggestion
{
    /// <summary>
    /// The source's ID of the other entry: the one suggested, or the one
    /// suggesting it.
    /// </summary>
    [Required]
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// What kind of entry the other entry is.
    /// </summary>
    [Required]
    public MetadataEntityType Type { get; init; } = null!;

    /// <summary>
    /// The other entry's title, if it is stored.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// What kind of suggestion it is.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public SuggestionKind Kind { get; init; }

    /// <summary>
    /// The source that gave the suggestion.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// Where the suggestion ranks among the entry's suggestions, if the
    /// source orders them.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// How many of those who voted agreed, if the source says.
    /// </summary>
    public double? ApprovalRating { get; init; }

    /// <summary>
    /// How many voted on the suggestion, if the source says.
    /// </summary>
    public int? Votes { get; init; }

    /// <summary>
    /// The source's own score for the suggestion, if it gives one.
    /// </summary>
    public int? Score { get; init; }
}

/// <summary>
/// Where an episode of any metadata source sits in one ordering of its series.
/// </summary>
public class MetadataEpisodeOrdering
{
    /// <summary>
    /// The ordering's full identifier.
    /// </summary>
    [Required]
    public string OrderingID { get; init; } = string.Empty;

    /// <summary>
    /// The full identifier of the season or group the episode sits in, if
    /// any.
    /// </summary>
    public string? SeasonID { get; init; }

    /// <summary>
    /// The number of the season or group the episode sits in, if any.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    /// The episode's number in the ordering.
    /// </summary>
    [Required]
    public int EpisodeNumber { get; init; }

    /// <summary>
    /// What type of episode it is in the ordering.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public EpisodeType EpisodeType { get; init; }

    /// <summary>
    /// The season of the regular episode a placed special airs before, when
    /// one of the group it airs in follows it. Set only on a placed special's
    /// place, which is in the special group.
    /// </summary>
    public int? AirsBeforeSeasonNumber { get; init; }

    /// <summary>
    /// The number of the regular episode a placed special airs before,
    /// within <see cref="AirsBeforeSeasonNumber"/>.
    /// </summary>
    public int? AirsBeforeEpisodeNumber { get; init; }

    /// <summary>
    /// The season a placed special airs after, when no regular episode of
    /// that season follows it.
    /// </summary>
    public int? AirsAfterSeasonNumber { get; init; }

    /// <summary>
    /// The full identifier of the regular episode a placed special airs right
    /// after, in any group.
    /// </summary>
    public string? AirsAfterEpisodeID { get; init; }

    /// <summary>
    /// The full identifier of the regular episode a placed special airs right
    /// before, in any group.
    /// </summary>
    public string? AirsBeforeEpisodeID { get; init; }

    /// <summary>
    /// Whether the ordering is the source's own.
    /// </summary>
    [Required]
    public bool IsDefault { get; init; }

    /// <summary>
    /// Whether the ordering is the one chosen for the series.
    /// </summary>
    [Required]
    public bool IsPreferred { get; init; }

    /// <summary>
    /// When the episode was placed in the ordering.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the episode's place in the ordering last changed.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }
}
