using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_SimilarRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Similar, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Similar, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_Similar, int>? _similarAnimeIDs;

    protected override int SelectKey(AniDB_Anime_Similar entity)
        => entity.AniDB_Anime_SimilarID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _similarAnimeIDs = Cache.CreateIndex(a => a.SimilarAnimeID);
    }

    /// <summary>
    /// The anime AniDB's users find similar to <paramref name="animeID"/>, in
    /// AniDB's own order.
    /// </summary>
    public List<AniDB_Anime_Similar> GetByAnimeID(int animeID)
        => _animeIDs!.GetMultiple(animeID)
            .OrderBy(a => a.Ordering)
            .ToList();

    /// <summary>
    /// The anime that <paramref name="similarAnimeID"/> is found similar to,
    /// best approved first. Works whether or not that anime is in the
    /// collection.
    /// </summary>
    public List<AniDB_Anime_Similar> GetBySimilarAnimeID(int similarAnimeID)
        => _similarAnimeIDs!.GetMultiple(similarAnimeID)
            .OrderByDescending(a => a.Total is 0 ? 0 : a.Approval / (double)a.Total)
            .ToList();
}
