using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AnimeCharacterRepository : BaseCachedRepository<AnimeCharacter, int>
{
    private PocoIndex<int, AnimeCharacter, int> AniDBIDs;

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AnimeCharacter entity)
    {
        return entity.CharacterID;
    }

    public override void PopulateIndexes()
    {
        AniDBIDs = new PocoIndex<int, AnimeCharacter, int>(Cache, a => a.AniDBID);
    }


    public AnimeCharacter GetByAniDBID(int id)
    {
        return ReadLock(() => AniDBIDs.GetOne(id));
    }

    public AnimeCharacterRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
