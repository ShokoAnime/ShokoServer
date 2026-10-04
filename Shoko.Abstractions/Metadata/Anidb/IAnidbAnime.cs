using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB anime.
/// </summary>
public interface IAnidbAnime : ISeries<IAnidbAnime, IAnidbEpisode>
{
    /// <summary>
    ///   The AniDB anime ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int AnidbID { get; }

    /// <summary>
    /// My Anime List (MAL) IDs linked to the AniDB anime.
    /// </summary>
    IReadOnlyList<int> MalIDs { get; }

    /// <summary>
    /// All tags for the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbTagForAnime> Tags { get; }

    /// <summary>
    ///   All release group statuses for the AniDB anime.
    /// </summary>
    IReadOnlyList<IAnidbReleaseGroupStatus> ReleaseGroupStatuses { get; }
}
