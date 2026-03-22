using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Anidb;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// Auto-magic AniDB to TMDB match result.
/// </summary>
public interface ITmdbAutoSearchResult
{
    /// <summary>
    /// Indicates that this is a local match using existing data instead of a
    /// remote match.
    /// </summary>
    bool IsLocal { get; }

    /// <summary>
    /// Indicates that this is a remote match.
    /// </summary>
    bool IsRemote { get; }

    /// <summary>
    /// The match rating of the result.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    /// Indicates that the result is for a movie auto-magic match.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AnidbEpisode))]
    [MemberNotNullWhen(true, nameof(TmdbMovie))]
    [MemberNotNullWhen(false, nameof(TmdbShow))]
    bool IsMovie { get; }

    /// <summary>
    /// The AniDB anime associated with the search result.
    /// </summary>
    IAnidbAnime AnidbAnime { get; }

    /// <summary>
    /// The AniDB episode associated with the search result, if it's a movie match.
    /// </summary>
    IAnidbEpisode? AnidbEpisode { get; }

    /// <summary>
    /// The TMDB show search result, if it's a show match.
    /// </summary>
    ITmdbShowSearchResult? TmdbShow { get; }

    /// <summary>
    /// The TMDB movie search result, if it's a movie match.
    /// </summary>
    ITmdbMovieSearchResult? TmdbMovie { get; }
}
