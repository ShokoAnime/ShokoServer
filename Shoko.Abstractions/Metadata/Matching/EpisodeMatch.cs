using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   One AniDB episode and what a source lines it up with.
/// </summary>
/// <remarks>
///   Not a cross-reference: what the matching came up with, which may never
///   be written.
/// </remarks>
public sealed record EpisodeMatch
{
    /// <summary>
    ///   The AniDB episode.
    /// </summary>
    public required IAnidbEpisode AnidbEpisode { get; init; }

    /// <summary>
    ///   What the source lines it up with, or <c>null</c> when
    ///   nothing did.
    /// </summary>
    /// <remarks>
    ///   Unmatched episodes are returned rather than dropped, so a preview
    ///   shows what it could not place.
    /// </remarks>
    public required IEpisode? Candidate { get; init; }

    /// <summary>
    ///   How the pairing was arrived at, and how much to trust it.
    ///   <see cref="MatchRating.None"/> where nothing matched.
    /// </summary>
    public required MatchRating Rating { get; init; }

    /// <summary>
    ///   Where this pairing sits when one episode lines up with several.
    /// </summary>
    public int Ordering { get; init; }
}
