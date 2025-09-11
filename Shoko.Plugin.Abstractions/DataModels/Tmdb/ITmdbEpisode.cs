
namespace Shoko.Plugin.Abstractions.DataModels.Tmdb;

/// <summary>
/// A TMDB episode.
/// </summary>
public interface ITmdbEpisode : IEpisode
{
    /// <summary>
    /// Indicates the episode is hidden by the user.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Get the tmdb show info for the episode, if available.
    /// </summary>
    new ITmdbShow? Series { get; }
}
