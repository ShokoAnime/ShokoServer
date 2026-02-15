using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Episode and a TMDB Movie.
/// </summary>
public interface ITmdbMovieCrossReference : IWithImages
{
    /// <summary>
    ///   The AniDB Anime ID for the AniDB Episode.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The AniDB Episode ID.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    ///   The TMDB Movie ID.
    /// </summary>
    int TmdbMovieID { get; }

    /// <summary>
    ///   The match rating for the cross-reference.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    ///   The Shoko Series for the cross-reference, if available.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }

    /// <summary>
    ///   The Shoko Episode for the cross-reference, if available.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }

    /// <summary>
    ///   The TMDB Movie for the cross-reference, if available.
    /// </summary>
    ITmdbMovie? TmdbMovie { get; }
}
