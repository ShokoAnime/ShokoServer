using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Anidb;

/// <summary>
/// An AniDB anime.
/// </summary>
public interface IAnidbAnime : ISeries
{
    /// <summary>
    /// All episodes for the the anidb anime.
    /// </summary>
    new IReadOnlyList<IAnidbEpisode> Episodes { get; }
}
