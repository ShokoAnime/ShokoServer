
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// A search result from the local AniDB title cache.
/// </summary>
public interface IAnidbAnimeSearchResult : IMetadata<int>, IWithTitles
{
    /// <summary>
    /// Indicates whether the search result is an exact match to the query.
    /// </summary>
    bool ExactMatch { get; }

    /// <summary>
    /// Represents the position of the match within the sanitized string.
    /// This property is only applicable when ExactMatch is set to true.
    /// A lower value indicates a match that occurs earlier in the string.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Represents the similarity measure between the sanitized query and the sanitized matched result.
    /// This may be the sorensen-dice distance or the tag weight when comparing tags for a series.
    /// A lower value indicates a more similar match.
    /// </summary>
    double Distance { get; }

    /// <summary>
    /// Represents the absolute difference in length between the sanitized query and the sanitized matched result.
    /// A lower value indicates a match with a more similar length to the query.
    /// </summary>
    int LengthDifference { get; }

    /// <summary>
    /// Contains the original matched substring from the original string.
    /// </summary>
    string MatchedTitle { get; }

    /// <summary>
    /// Full AniDB Anime entry, if available locally.
    /// </summary>
    ISeries? AnidbAnime { get; }

    /// <summary>
    /// Full Shoko Series entry, if available locally.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }
}
