using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Shoko;

/// <summary>
/// Shoko episode metadata.
/// </summary>
public interface IShokoEpisode : IEpisode
{
    /// <summary>
    /// The id of the anidb episode linked to the shoko episode.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// Indicates the episode is hidden by the user.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Get the shoko series info for the episode, if available.
    /// </summary>
    new IShokoSeries? Series { get; }

    /// <summary>
    /// A direct link to the anidb episode metadata.
    /// </summary>
    IEpisode AnidbEpisode { get; }

    /// <summary>
    /// A direct link to all tmdb episodes linked to the shoko episode.
    /// </summary>
    IReadOnlyList<IEpisode> TmdbEpisodes { get; }

    /// <summary>
    /// A direct link to all tmdb movies linked to the shoko episode.
    /// </summary>
    IReadOnlyList<IMovie> TmdbMovies { get; }

    /// <summary>
    /// All episodes linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IEpisode> LinkedEpisodes { get; }

    /// <summary>
    /// All movies linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }
}
