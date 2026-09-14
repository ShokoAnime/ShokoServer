using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// Auto-magic AniDB to Anilist match result.
/// </summary>
public interface IAnilistAutoSearchResult
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
    /// The AniDB anime associated with the search result.
    /// </summary>
    IAnidbAnime AnidbAnime { get; }

    /// <summary>
    /// The Anilist anime search result.
    /// </summary>
    IAnilistAnimeSearchResult AnilistAnime { get; }
}
