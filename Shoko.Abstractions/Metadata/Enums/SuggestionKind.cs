namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// What a suggestion claims about the entity it points at.
/// </summary>
public enum SuggestionKind
{
    /// <summary>
    /// Watch this next if you liked the base entity. TMDB's recommendations,
    /// and those of a plugin source such as AniList.
    /// </summary>
    Recommended = 0,

    /// <summary>
    /// This resembles the base entity, whoever judged it. AniDB's similar
    /// anime and TMDB's similar lists.
    /// </summary>
    Similar = 1,
}
