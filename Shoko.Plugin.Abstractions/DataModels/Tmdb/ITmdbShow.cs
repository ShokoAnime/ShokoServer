using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Tmdb;

/// <summary>
/// A TMDB show.
/// </summary>
public interface ITmdbShow : ISeries
{
    /// <summary>
    /// All episodes for the the tmdb show.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }
}
