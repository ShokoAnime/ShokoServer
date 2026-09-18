using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_StaffRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Staff, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Staff, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Staff, int>? _anilistCreatorIDs;

    protected override int SelectKey(Anilist_Anime_Staff entity)
        => entity.Anilist_Anime_StaffID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistCreatorIDs = Cache.CreateIndex(a => a.AnilistCreatorID);
    }

    public IReadOnlyList<Anilist_Anime_Staff> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(x => x.Ordering)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Staff> GetByAnilistCreatorID(int anilistCreatorId)
        => _anilistCreatorIDs!.GetMultiple(anilistCreatorId)
            .OrderBy(x => x.AnilistAnimeID)
            .ThenBy(x => x.Ordering)
            .ToList();
}
