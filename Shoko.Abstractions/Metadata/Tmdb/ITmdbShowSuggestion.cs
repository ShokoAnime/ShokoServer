namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A show TMDB suggests to someone looking at another show, either as a
/// recommendation or as a similar title, as
/// <see cref="ISuggestedMetadata.Kind"/> says. TMDB hands out an ordered list
/// and no votes, so <see cref="ISuggestedMetadata.Order"/> is what ranks them.
/// </summary>
public interface ITmdbShowSuggestion : ISuggestedMetadata<ITmdbShow, ITmdbShow>
{
}
