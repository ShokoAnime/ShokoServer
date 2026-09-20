using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_SuggestionRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Suggestion, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Suggestion, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Suggestion, int>? _suggestedAnilistAnimeIDs;

    protected override int SelectKey(Anilist_Anime_Suggestion entity)
        => entity.Anilist_Anime_SuggestionID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _suggestedAnilistAnimeIDs = Cache.CreateIndex(a => a.SuggestedAnilistAnimeID);
    }

    /// <summary>
    /// The anime AniList recommends for <paramref name="anilistAnimeId"/>, best first.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Suggestion> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(x => x.Ordering)
            .ToList();

    /// <summary>
    /// The anime that recommend <paramref name="anilistAnimeId"/>, which works
    /// whether or not that anime is in the collection.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Suggestion> GetBySuggestedAnilistAnimeID(int anilistAnimeId)
        => _suggestedAnilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderByDescending(x => x.Rating)
            .ToList();

    /// <summary>
    /// Every recommendation involving <paramref name="anilistAnimeId"/>, from
    /// both stored directions, expressed as that anime's own suggestions and
    /// ordered best first.
    /// </summary>
    /// <remarks>
    /// AniList holds one undirected recommendation and serves it from both
    /// sides with the same score, so an edge stored while fetching the other
    /// anime is this anime's recommendation too. Merging matters rather than
    /// merely tidying, because how far
    /// <see cref="Settings.AnilistSettings.RecommendationDepth"/> pages means
    /// an edge can sit above the cutoff on one side and below it on the other.
    ///
    /// Where both copies were stored the direct one wins, since only it
    /// carries this side's own <see cref="Anilist_Anime_Suggestion.Ordering"/>.
    ///
    /// This is the one place the merge lives. Counting the rows of a single
    /// direction anywhere else would disagree with what the API returns.
    /// </remarks>
    public IReadOnlyList<Anilist_Anime_Suggestion> GetMergedByAnilistAnimeID(int anilistAnimeId)
        => GetByAnilistAnimeID(anilistAnimeId)
            .Concat(GetBySuggestedAnilistAnimeID(anilistAnimeId).Select(suggestion => suggestion.Reversed))
            .DistinctBy(suggestion => suggestion.SuggestedAnilistAnimeID)
            .OrderByDescending(suggestion => suggestion.Rating)
            .ThenBy(suggestion => suggestion.SuggestedAnilistAnimeID)
            .ToList();
}
