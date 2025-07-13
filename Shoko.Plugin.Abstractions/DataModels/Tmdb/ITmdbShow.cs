using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Tmdb;

/// <summary>
/// A TMDB show.
/// </summary>
public interface ITmdbShow : ISeries
{
    /// <summary>
    /// The keywords for the TMDB show.
    /// </summary>
    IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// The genres for the TMDB show.
    /// </summary>
    IReadOnlyList<string> Genres { get; }

    /// <summary>
    /// All episodes for the the TMDB show.
    /// </summary>
    new IReadOnlyList<ITmdbEpisode> Episodes { get; }
}
