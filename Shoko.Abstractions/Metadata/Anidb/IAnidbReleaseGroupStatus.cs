
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
///   The release status of a single AniDB release group for an
///   anime, as reported by AniDB's group status data.
/// </summary>
public interface IAnidbReleaseGroupStatus
{
    /// <summary>
    ///   The ID of the AniDB anime this status is for.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   A direct link to the AniDB anime metadata, if available.
    /// </summary>
    IAnidbAnime? AnidbAnime { get; }

    /// <summary>
    ///   The AniDB group ID.
    /// </summary>
    int GroupID { get; }

    /// <summary>
    ///   The release group, resolved from locally known release
    ///   info when possible, or a minimal stand-in built from the
    ///   group status data otherwise.
    /// </summary>
    IReleaseGroup? Group { get; }

    /// <summary>
    ///   The group's completion status for the anime.
    /// </summary>
    GroupCompletionStatus CompletionState { get; }

    /// <summary>
    ///   The last episode number released by the group, or 0 if
    ///   unknown. Kept as a raw number for plugins that don't need
    ///   the resolved <see cref="LastEpisode"/>.
    /// </summary>
    int LastEpisodeNumber { get; }

    /// <summary>
    ///   The last episode released by the group, if it is known
    ///   locally.
    /// </summary>
    IAnidbEpisode? LastEpisode { get; }

    /// <summary>
    ///   The raw, comma-separated AniDB episode codes released by
    ///   the group (e.g. "1,2,3,S1"), as reported by AniDB. Kept for
    ///   plugins that don't need the resolved
    ///   <see cref="ReleasedEpisodes"/>.
    /// </summary>
    string EpisodeRange { get; }

    /// <summary>
    ///   All episodes released by the group that are known
    ///   locally.
    /// </summary>
    IReadOnlyList<IAnidbEpisode> ReleasedEpisodes { get; }

    /// <summary>
    ///   The group's rating for their release of the anime, on a
    ///   scale of 0-10, or 0 if unrated.
    /// </summary>
    double Rating { get; }

    /// <summary>
    ///   The number of votes behind <see cref="Rating"/>.
    /// </summary>
    int RatingVotes { get; }
}
