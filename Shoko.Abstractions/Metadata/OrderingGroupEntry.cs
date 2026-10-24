namespace Shoko.Abstractions.Metadata;

/// <summary>
///   One entry a group of an ordering lists: a home episode with its number,
///   or a placed special, flagged as a special with its number in the special
///   group. The special group's entries also say where they air.
/// </summary>
public sealed record OrderingGroupEntry
{
    /// <summary>
    ///   The episode.
    /// </summary>
    public required MetadataGuid EpisodeID { get; init; }

    /// <summary>
    ///   The episode's number: among the group's home episodes, from 1, or
    ///   its number in the special group for a special. The default ordering
    ///   keeps each episode's own number.
    /// </summary>
    public required int EpisodeNumber { get; init; }

    /// <summary>
    ///   Whether the episode is a special here: it is in the special group,
    ///   or it is a placed special airing in this regular group.
    /// </summary>
    public required bool IsSpecial { get; init; }

    /// <summary>
    ///   The season of the regular episode a placed special airs before. Set
    ///   only on a placed special's entry in the special group.
    /// </summary>
    public int? AirsBeforeSeasonNumber { get; init; }

    /// <summary>
    ///   The number of the regular episode a placed special airs before.
    /// </summary>
    public int? AirsBeforeEpisodeNumber { get; init; }

    /// <summary>
    ///   The season a placed special airs after, when no episode of it
    ///   follows the special.
    /// </summary>
    public int? AirsAfterSeasonNumber { get; init; }

    /// <summary>
    ///   The regular episode a placed special airs right after, in any group.
    /// </summary>
    public MetadataGuid? AirsAfterEpisodeID { get; init; }

    /// <summary>
    ///   The regular episode a placed special airs right before, in any group.
    /// </summary>
    public MetadataGuid? AirsBeforeEpisodeID { get; init; }
}
