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
}
