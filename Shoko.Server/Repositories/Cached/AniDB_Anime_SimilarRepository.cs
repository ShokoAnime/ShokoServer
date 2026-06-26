#nullable enable
using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_SimilarRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Similar, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Similar, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_Similar, (int AnimeID, int SimilarID)>? _pairedIDs;

    protected override int SelectKey(AniDB_Anime_Similar entity)
        => entity.AniDB_Anime_SimilarID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _pairedIDs = Cache.CreateIndex(a => (a.AnimeID, a.SimilarAnimeID));
    }

    public List<AniDB_Anime_Similar> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public AniDB_Anime_Similar? GetByAnimeIDAndSimilarID(int animeID, int similarAnimeID)
        => ReadLock(() => _pairedIDs!.GetOne((animeID, similarAnimeID)));
}
