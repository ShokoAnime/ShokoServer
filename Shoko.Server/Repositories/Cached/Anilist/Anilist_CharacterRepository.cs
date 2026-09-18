using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_CharacterRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Character, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Character, int>? _anilistCharacterIDs;

    protected override int SelectKey(Anilist_Character entity)
        => entity.Anilist_CharacterID;

    public override void PopulateIndexes()
    {
        _anilistCharacterIDs = Cache.CreateIndex(a => a.AnilistCharacterID);
    }

    public Anilist_Character? GetByAnilistCharacterID(int anilistCharacterId)
        => _anilistCharacterIDs!.GetOne(anilistCharacterId);
}
