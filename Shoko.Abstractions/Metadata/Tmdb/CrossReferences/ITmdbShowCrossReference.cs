using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

/// <summary>
///  A Cross-reference between an AniDB Anime and a TMDB Show.
/// </summary>
public interface ITmdbShowCrossReference : IWithImages
{
    /// <summary>
    ///   The AniDB Anime ID.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The TMDB Show ID.
    /// </summary>
    int TmdbShowID { get; }

    /// <summary>
    ///   The match rating for the cross-reference.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    ///   The Shoko Series for the cross-reference, if available.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }

    /// <summary>
    ///   The TMDB Show for the cross-reference, if available.
    /// </summary>
    ITmdbShow? TmdbShow { get; }
}
