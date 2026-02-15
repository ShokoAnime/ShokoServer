using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// TMDB episode ordering information.
/// </summary>
public interface ITmdbEpisodeOrderingInformation : IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The TMDB show ID.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// The TMDB ordering ID.
    /// </summary>
    string OrderingID { get; }

    /// <summary>
    /// The TMDB season ID within the ordering schema.
    /// </summary>
    string SeasonID { get; }

    /// <summary>
    /// The TMDB episode ID.
    /// </summary>
    int EpisodeID { get; }

    /// <summary>
    /// The season number within the ordering schema.
    /// </summary>
    int SeasonNumber { get; }

    /// <summary>
    /// The episode number within the ordering schema.
    /// </summary>
    int EpisodeNumber { get; }

    /// <summary>
    /// Indicates the current ordering is the default ordering for the show.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    /// Indicates the current ordering is the preferred ordering for the show.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    /// The TMDB show, if available.
    /// </summary>
    ITmdbShow? Series { get; }

    /// <summary>
    /// The TMDB season within the ordering schema, if available.
    /// </summary>
    ITmdbSeason? Season { get; }

    /// <summary>
    /// The TMDB episode this information is for.
    /// </summary>
    ITmdbEpisode Episode { get; }
}
