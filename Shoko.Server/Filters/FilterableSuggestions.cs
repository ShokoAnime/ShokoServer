using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters;

/// <summary>
/// The provider entities a filterable reaches its suggestions through, gathered
/// once so the counts below do not re-walk the cross-references for every read.
/// </summary>
/// <param name="AnidbAnimeIDs">The AniDB anime linked to the filterable.</param>
/// <param name="LinkedEntries">The series and movies linked on every other source.</param>
internal sealed record SuggestionSources(
    IReadOnlyList<int> AnidbAnimeIDs,
    IReadOnlyList<MetadataGuid> LinkedEntries
);

/// <summary>
/// Counts the suggestions behind the suggestion filter expressions, shared by
/// the series and the group filterable so the two cannot drift apart.
/// </summary>
/// <remarks>
/// Every count is of the suggestions the filterable <em>makes</em>, never of
/// the ones pointing at it. Every source is read from cached repositories,
/// AniDB from its own and the others from the shared suggestion store, since
/// a filter evaluates these once per entry in the collection and a database
/// read would crawl.
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
    /// The number of suggestions a source other than AniDB makes for the
    /// linked series and movies.
    /// </summary>
    /// <param name="sources">The linked provider entities.</param>
    /// <param name="source">The source.</param>
    /// <returns>The count.</returns>
    public static int CountOther(SuggestionSources sources, MetadataSource source)
        => sources.LinkedEntries.Where(entry => entry.Source == source).Sum(entry => SuggestionsOf(entry).Count);

    /// <summary>
    /// The number of suggestions every source other than AniDB makes for the
    /// linked series and movies.
    /// </summary>
    /// <param name="sources">The linked provider entities.</param>
    /// <returns>The count.</returns>
    public static int CountOthers(SuggestionSources sources)
        => sources.LinkedEntries.Sum(entry => SuggestionsOf(entry).Count);

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

        foreach (var entry in sources.LinkedEntries)
            count += SuggestionsOf(entry).Count(suggestion => IsLinkedInCollection(suggestion));

        return count;
    }

    #endregion

    #region Collection lookups

    /// <summary>
    /// The suggestions stored for a linked series or movie.
    /// </summary>
    /// <param name="entry">The series or movie.</param>
    /// <returns>The suggestions.</returns>
    private static IReadOnlyList<Metadata_Suggestion> SuggestionsOf(MetadataGuid entry)
        => RepoFactory.Metadata_Suggestion.GetByBase(entry);

    /// <summary>
    /// Whether an AniDB anime has a series in the collection.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>Whether it is in the collection.</returns>
    private static bool IsInCollection(int anidbAnimeID)
        => RepoFactory.AnimeSeries.GetByAnimeID(anidbAnimeID) is not null;

    /// <summary>
    /// Whether the series or movie a suggestion names is linked to a series in
    /// the collection.
    /// </summary>
    /// <param name="suggestion">The suggestion.</param>
    /// <returns>Whether it is in the collection.</returns>
    private static bool IsLinkedInCollection(Metadata_Suggestion suggestion)
        => suggestion.SuggestedType == MetadataEntityType.Movie
            ? RepoFactory.CrossRef_AniDB_Metadata_Movie.GetByProviderID(suggestion.Source, suggestion.SuggestedID).Any(xref => IsInCollection(xref.AnidbAnimeID))
            : RepoFactory.CrossRef_AniDB_Metadata_Series.GetByProviderID(suggestion.Source, suggestion.SuggestedID).Any(xref => IsInCollection(xref.AnidbAnimeID));

    #endregion
}
