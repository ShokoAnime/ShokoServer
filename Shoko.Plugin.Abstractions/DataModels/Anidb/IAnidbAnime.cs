using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Anidb;

/// <summary>
/// An AniDB anime.
/// </summary>
public interface IAnidbAnime : ISeries
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
    /// All episodes for the the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbEpisode> Episodes { get; }
}
