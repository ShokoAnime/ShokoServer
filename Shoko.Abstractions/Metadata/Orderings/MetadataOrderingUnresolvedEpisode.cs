using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   An episode an ordering import could not find in the series.
/// </summary>
/// <param name="GroupIndex">The group's place in the ordering, from 0.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="AnidbEpisodeID">The AniDB episode the payload names, if any.</param>
/// <param name="EpisodeType">The episode's type in the payload, if given.</param>
/// <param name="EpisodeNumber">The episode's number in the payload, if given.</param>
/// <param name="Reason">Why it could not be found.</param>
public sealed record MetadataOrderingUnresolvedEpisode(
    int GroupIndex,
    string GroupName,
    int? AnidbEpisodeID,
    EpisodeType? EpisodeType,
    int? EpisodeNumber,
    string Reason
);
