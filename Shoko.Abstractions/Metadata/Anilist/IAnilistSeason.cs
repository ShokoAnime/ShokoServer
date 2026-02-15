using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// Fake "season" for the AniList anime.
/// </summary>
public interface IAnilistSeason : ISeason, IWithUpdateDate
{
    /// <summary>
    /// Get the AniList anime info for the "season," if available.
    /// </summary>
    new IAnilistAnime Series { get; }

    /// <summary>
    /// All episodes for the AniList anime for the fake "season."
    /// </summary>
    new IReadOnlyList<IAnilistEpisode> Episodes { get; }
}
