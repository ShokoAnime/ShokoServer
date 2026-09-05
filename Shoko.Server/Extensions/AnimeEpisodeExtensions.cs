using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Extensions;

/// <summary>
/// Extension methods for <see cref="AnimeEpisode"/>.
/// </summary>
public static class AnimeEpisodeExtensions
{
    /// <summary>
    /// Determines whether an aired episode with no local files should be counted as missing,
    /// based on the cached AniDB group release statuses for its anime.
    /// </summary>
    /// <param name="episode">The episode to evaluate.</param>
    /// <param name="groupStatuses">Group statuses already scoped to the episode's anime. An empty list is treated as missing.</param>
    /// <returns><see langword="true"/> if the episode is considered missing; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This predicate is intended for <see cref="EpisodeType.Episode"/> episodes only; callers must
    /// pre-filter by episode type AND file presence before calling it.
    /// </remarks>
    public static bool IsMissingEpisode(this AnimeEpisode episode, IReadOnlyList<AniDB_GroupStatus> groupStatuses)
    {
        if (episode.IsHidden) return false;
        var anidb = episode.AniDB_Episode;
        if (anidb == null) return false;
        if (!anidb.HasAired) return false;

        return groupStatuses.Count == 0 || groupStatuses.Any(gs =>
            gs.CompletionState is (int)GroupCompletionStatus.Complete or (int)GroupCompletionStatus.Finished
            || gs.LastEpisodeNumber >= anidb.EpisodeNumber);
    }
}
