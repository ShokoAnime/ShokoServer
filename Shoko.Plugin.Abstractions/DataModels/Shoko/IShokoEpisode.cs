using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Shoko;

public interface IShokoEpisode : IEpisode
{
    /// <summary>
    /// The id of the anidb episode linked to the shoko episode.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// Get the shoko series info for the episode, if available.
    /// </summary>
    new IShokoSeries? SeriesInfo { get; }

    /// <summary>
    /// A direct link to the anidb episode metadata.
    /// </summary>
    IEpisode AnidbEpisode { get; }

    /// <summary>
    /// All episodes linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IEpisode> LinkedEpisodes { get; }

    /// <summary>
    /// All movies linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }
}
