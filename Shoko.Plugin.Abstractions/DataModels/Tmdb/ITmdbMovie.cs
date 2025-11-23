
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Tmdb;

/// <summary>
/// A TMDB movie.
/// </summary>
public interface ITmdbMovie : IMovie
{
    /// <summary>
    /// Gets the keywords for the TMDB movie.
    /// </summary>
    IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// Gets the genres for the TMDB movie.
    /// </summary>
    IReadOnlyList<string> Genres { get; }
}
