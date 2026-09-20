namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// What a suggestion claims about the entity it points at.
/// </summary>
public enum SuggestionKind
{
    /// <summary>
    /// Watch this next if you liked the base entity. AniList's and TMDB's
    /// recommendations.
    /// </summary>
    Recommended = 0,

    /// <summary>
    /// This resembles the base entity, whoever judged it. AniDB's similar
    /// anime and TMDB's similar lists.
    /// </summary>
    Similar = 1,
}
