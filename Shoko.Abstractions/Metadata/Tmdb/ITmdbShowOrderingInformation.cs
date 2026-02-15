using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb.Enums;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// TMDB show ordering information.
/// </summary>
public interface ITmdbShowOrderingInformation : IWithCastAndCrew, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The TMDB show ID.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// The ordering ID.
    /// </summary>
    string OrderingID { get; }

    /// <summary>
    /// The alternate ordering type.
    /// </summary>
    TmdbAlternateOrderingType OrderingType { get; }

    /// <summary>
    /// English name of the ordering scheme.
    /// </summary>
    string OrderingName { get; }

    /// <summary>
    /// Description of the ordering scheme.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The number of episodes in the ordering scheme.
    /// </summary>
    int EpisodeCount { get; }

    /// <summary>
    /// The number of hidden episodes in the ordering scheme.
    /// </summary>
    int HiddenEpisodeCount { get; }

    /// <summary>
    /// The number of seasons in the ordering scheme.
    /// </summary>
    int SeasonCount { get; }

    /// <summary>
    /// Indicates the current ordering is the preferred ordering for the show.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    /// The TMDB show, if available.
    /// </summary>
    ITmdbShow? Series { get; }

    /// <summary>
    /// The seasons in the ordering scheme.
    /// </summary>
    IReadOnlyList<ITmdbSeason> Seasons { get; }

    /// <summary>
    /// The episodes in the ordering scheme.
    /// </summary>
    IReadOnlyList<ITmdbEpisode> Episodes { get; }
}
