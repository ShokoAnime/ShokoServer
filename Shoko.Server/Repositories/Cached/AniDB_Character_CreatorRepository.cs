using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AniDB_Character_CreatorRepository : BaseCachedRepository<AniDB_Character_Creator, int>
{
    private PocoIndex<int, AniDB_Character_Creator, int>? _charIDs;

    public List<AniDB_Character_Creator> GetByCharacterID(int characterID)
    {
        return ReadLock(() => _charIDs!.GetMultiple(characterID));
    }

    public List<AniDB_Character_Creator> GetByCreatorID(int creatorID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Character_Creator>()
                .Where(a => a.CreatorID == creatorID)
                .ToList();
        });
    }

    public override void PopulateIndexes()
    {
        _charIDs = new PocoIndex<int, AniDB_Character_Creator, int>(Cache, a => a.CharacterID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_Character_Creator entity)
    {
        return entity.AniDB_Character_CreatorID;
    }

    public AniDB_Character_CreatorRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
