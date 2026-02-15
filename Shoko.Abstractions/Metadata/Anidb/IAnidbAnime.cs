using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB anime.
/// </summary>
public interface IAnidbAnime : ISeries, IWithUpdateDate
{
    /// <summary>
    /// My Anime List (MAL) IDs linked to the AniDB anime.
    /// </summary>
    IReadOnlyList<int> MalIDs { get; }

    /// <summary>
    /// All tags for the AniDB anime.
    /// </summary>
    IReadOnlyList<IAnidbTagForAnime> Tags { get; }

    /// <summary>
    /// All known fake "seasons" for the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbEpisode> Episodes { get; }
}
