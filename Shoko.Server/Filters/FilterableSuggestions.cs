using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters;

/// <summary>
/// The provider entities a filterable reaches its suggestions through, gathered
/// once so the counts below do not re-walk the cross-references for every read.
/// </summary>
/// <param name="AnidbAnimeIDs">The AniDB anime linked to the filterable.</param>
/// <param name="TmdbShowIDs">The TMDB shows linked to the filterable.</param>
/// <param name="TmdbMovieIDs">The TMDB movies linked to the filterable.</param>
/// <param name="AnilistAnimeIDs">The AniList anime linked to the filterable.</param>
internal sealed record SuggestionSources(
    IReadOnlyList<int> AnidbAnimeIDs,
    IReadOnlyList<int> TmdbShowIDs,
    IReadOnlyList<int> TmdbMovieIDs,
    IReadOnlyList<int> AnilistAnimeIDs
);

/// <summary>
/// Counts the suggestions behind the suggestion filter expressions, shared by
/// the series and the group filterable so the two cannot drift apart.
/// </summary>
/// <remarks>
/// Every count is of the suggestions the filterable <em>makes</em>, never of
/// the ones pointing at it. All four sources are read from cached
/// repositories, because a filter evaluates these once per entry in the
/// collection and anything hitting the database here would crawl.
/// </remarks>
internal static class FilterableSuggestions
{
    #region Counts

    /// <summary>
    /// The number of similar anime AniDB lists for the linked anime.
    /// </summary>
    /// <param name="sources">The linked provider entities.</param>
    /// <returns>The count.</returns>
    public static int CountAnidb(SuggestionSources sources)
        => sources.AnidbAnimeIDs.Sum(animeID => RepoFactory.AniDB_Anime_Similar.GetByAnimeID(animeID).Count);

    /// <summary>
    /// The number of recommendations and similar titles TMDB lists for the
    /// linked shows and movies.
    /// </summary>
    /// <param name="sources">The linked provider entities.</param>
    /// <returns>The count.</returns>
    public static int CountTmdb(SuggestionSources sources)
        => sources.TmdbShowIDs.Sum(showID => RepoFactory.TMDB_Suggestion.GetByTmdbEntityID(DataEntityType.Show, showID).Count) +
            sources.TmdbMovieIDs.Sum(movieID => RepoFactory.TMDB_Suggestion.GetByTmdbEntityID(DataEntityType.Movie, movieID).Count);

    /// <summary>
    /// The number of recommendations AniList lists for the linked anime.
    /// </summary>
    /// <param name="sources">The linked provider entities.</param>
    /// <returns>The count.</returns>
    /// <remarks>
    /// Counted through <c>GetMergedByAnilistAnimeID</c>, because AniList's
    /// edges are stored in whichever direction they were fetched and belong to
    /// both ends. Counting one direction's rows would disagree with what
    /// <c>/api/v3/Series/{id}/Suggested</c> returns.
    /// </remarks>
    public static int CountAnilist(SuggestionSources sources)
        => sources.AnilistAnimeIDs.Sum(animeID => RepoFactory.Anilist_Anime_Suggestion.GetMergedByAnilistAnimeID(animeID).Count);

    /// <summary>
    /// The number of suggestions, from any source, whose other end traces back
    /// to a series in the collection.
    /// </summary>
    /// <param name="sources">The linked provider entities.</param>
    /// <returns>The count.</returns>
    public static int CountLocal(SuggestionSources sources)
    {
        var count = 0;
        foreach (var animeID in sources.AnidbAnimeIDs)
            count += RepoFactory.AniDB_Anime_Similar.GetByAnimeID(animeID)
                .Count(suggestion => IsInCollection(suggestion.SimilarAnimeID));

        foreach (var showID in sources.TmdbShowIDs)
            count += RepoFactory.TMDB_Suggestion.GetByTmdbEntityID(DataEntityType.Show, showID)
                .Count(suggestion => IsTmdbShowInCollection(suggestion.SuggestedTmdbEntityID));

        foreach (var movieID in sources.TmdbMovieIDs)
            count += RepoFactory.TMDB_Suggestion.GetByTmdbEntityID(DataEntityType.Movie, movieID)
                .Count(suggestion => IsTmdbMovieInCollection(suggestion.SuggestedTmdbEntityID));

        foreach (var animeID in sources.AnilistAnimeIDs)
            count += RepoFactory.Anilist_Anime_Suggestion.GetMergedByAnilistAnimeID(animeID)
                .Count(suggestion => IsAnilistInCollection(suggestion.SuggestedAnilistAnimeID));

        return count;
    }

    #endregion

    #region Collection lookups

    /// <summary>
    /// Whether an AniDB anime has a series in the collection.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>Whether it is in the collection.</returns>
    private static bool IsInCollection(int anidbAnimeID)
        => RepoFactory.AnimeSeries.GetByAnimeID(anidbAnimeID) is not null;

    /// <summary>
    /// Whether a TMDB show is linked to a series in the collection.
    /// </summary>
    /// <param name="tmdbShowID">The TMDB show ID.</param>
    /// <returns>Whether it is in the collection.</returns>
    private static bool IsTmdbShowInCollection(int tmdbShowID)
        => RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(tmdbShowID)
            .Any(xref => IsInCollection(xref.AnidbAnimeID));

    /// <summary>
    /// Whether a TMDB movie is linked to a series in the collection.
    /// </summary>
    /// <param name="tmdbMovieID">The TMDB movie ID.</param>
    /// <returns>Whether it is in the collection.</returns>
    private static bool IsTmdbMovieInCollection(int tmdbMovieID)
        => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(tmdbMovieID)
            .Any(xref => IsInCollection(xref.AnidbAnimeID));

    /// <summary>
    /// Whether an AniList anime is linked to a series in the collection.
    /// </summary>
    /// <param name="anilistAnimeID">The AniList anime ID.</param>
    /// <returns>Whether it is in the collection.</returns>
    private static bool IsAnilistInCollection(int anilistAnimeID)
        => RepoFactory.CrossRef_AniDB_Anilist_Anime.GetByAnilistAnimeID(anilistAnimeID)
            .Any(xref => IsInCollection(xref.AnidbAnimeID));

    #endregion
}
