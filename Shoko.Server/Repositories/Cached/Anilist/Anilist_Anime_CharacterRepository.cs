using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_Anime_CharacterRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime_Character, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime_Character, int>? _anilistAnimeIDs;

    private PocoIndex<int, Anilist_Anime_Character, int>? _anilistCharacterIDs;

    private PocoIndex<int, Anilist_Anime_Character, (int, int)>? _pairedIDs;

    protected override int SelectKey(Anilist_Anime_Character entity)
        => entity.Anilist_Anime_CharacterID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
        _anilistCharacterIDs = Cache.CreateIndex(a => a.AnilistCharacterID);
        _pairedIDs = Cache.CreateIndex(a => (a.AnilistAnimeID, a.AnilistCharacterID));
    }

    public IReadOnlyList<Anilist_Anime_Character> GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetMultiple(anilistAnimeId)
            .OrderBy(x => x.Ordering)
            .ToList();

    public IReadOnlyList<Anilist_Anime_Character> GetByAnilistCharacterID(int anilistCharacterId)
        => _anilistCharacterIDs!.GetMultiple(anilistCharacterId)
            .OrderBy(x => x.AnilistAnimeID)
            .ToList();

    public Anilist_Anime_Character? GetByAnilistAnimeAndCharacterIDs(int anilistAnimeId, int anilistCharacterId)
        => _pairedIDs!.GetOne((anilistAnimeId, anilistCharacterId));
}
