namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A movie TMDB suggests to someone looking at another movie, either as a
/// recommendation or as a similar title, as
/// <see cref="ISuggestedMetadata.Kind"/> says. TMDB hands out an ordered list
/// and no votes, so <see cref="ISuggestedMetadata.Order"/> is what ranks them.
/// </summary>
public interface ITmdbMovieSuggestion : ISuggestedMetadata<ITmdbMovie, ITmdbMovie>
{
}
