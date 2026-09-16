using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class CrossRef_AniDB_Anilist_AnimeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_Anilist_Anime, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_Anilist_Anime, int>? _anidbAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Anime, int>? _anilistAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_Anilist_Anime, (int, int)>? _pairedIDs;

    protected override int SelectKey(CrossRef_AniDB_Anilist_Anime entity)
        => entity.CrossRef_AniDB_Anilist_AnimeID;

    public override void PopulateIndexes()
    {
        _anidbAnimeIDs = Cache.CreateIndex(a => a.AnidbAnimeID);
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _pairedIDs = Cache.CreateIndex(a => (a.AnidbAnimeID, a.AnilistAnimeID));
    }

    public IReadOnlyList<CrossRef_AniDB_Anilist_Anime> GetByAnidbAnimeID(int animeId)
        => _anidbAnimeIDs!.GetMultiple(animeId);

    public IReadOnlyList<CrossRef_AniDB_Anilist_Anime> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId);

    public CrossRef_AniDB_Anilist_Anime? GetByAnidbAnimeAndAnilistAnimeIDs(int anidbId, int anilistId)
        => _pairedIDs!.GetOne((anidbId, anilistId));
}
