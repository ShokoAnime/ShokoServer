using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// Fake "season" for the AniDB anime.
/// </summary>
public interface IAnidbSeason : ISeason, IWithUpdateDate
{
    /// <summary>
    /// Get the AniDB anime info for the "season," if available.
    /// </summary>
    new IAnidbAnime Series { get; }

    /// <summary>
    /// All episodes for the AniDB anime for the fake "season."
    /// </summary>
    new IReadOnlyList<IAnidbEpisode> Episodes { get; }
}
