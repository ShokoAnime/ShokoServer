using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_Character_CreatorRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Character_Creator, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Character_Creator, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Character_Creator, int>? _anilistCreatorIDs;

    private PocoIndex<int, Anilist_Anime_Character_Creator, (int, int)>? _pairedIDs;

    protected override int SelectKey(Anilist_Anime_Character_Creator entity)
        => entity.Anilist_Anime_Character_CreatorID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistCreatorIDs = Cache.CreateIndex(a => a.AnilistCreatorID);
        _pairedIDs = Cache.CreateIndex(a => (a.AnilistAnimeID, a.AnilistCharacterID));
    }

    public IReadOnlyList<Anilist_Anime_Character_Creator> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(x => x.AnilistCharacterID)
            .ThenBy(x => x.Ordering)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Character_Creator> GetByAnilistCreatorID(int anilistCreatorId)
        => _anilistCreatorIDs!.GetMultiple(anilistCreatorId)
            .OrderBy(x => x.AnilistAnimeID)
            .ThenBy(x => x.Ordering)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Character_Creator> GetByAnilistAnimeAndCharacterIDs(int anilistAnimeId, int anilistCharacterId)
        => _pairedIDs!.GetMultiple((anilistAnimeId, anilistCharacterId))
            .OrderBy(x => x.Ordering)
            .ToList();
}
