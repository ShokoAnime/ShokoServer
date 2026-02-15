using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Episode and a TMDB Episode.
/// </summary>
public interface ITmdbEpisodeCrossReference : IWithImages
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
    ///   The TMDB Show ID for the TMDB Episode.
    /// </summary>
    int TmdbShowID { get; }

    /// <summary>
    ///   The TMDB Episode ID.
    /// </summary>
    int TmdbEpisodeID { get; }

    /// <summary>
    ///   The index to order the cross-references if multiple references exists
    ///   for the same anidb or tmdb episode.
    /// </summary>
    int Ordering { get; }

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
    ///   The TMDB Show for the cross-reference, if available.
    /// </summary>
    ITmdbShow? TmdbShow { get; }

    /// <summary>
    ///   The TMDB Episode for the cross-reference, if available.
    /// </summary>
    ITmdbEpisode? TmdbEpisode { get; }
}
