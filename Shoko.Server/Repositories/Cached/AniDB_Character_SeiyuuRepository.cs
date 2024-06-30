using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_Character_SeiyuuRepository : BaseCachedRepository<AniDB_Character_Seiyuu, int>
{
    private PocoIndex<int, AniDB_Character_Seiyuu, int> _charIDs;

    public List<AniDB_Character_Seiyuu> GetByCharID(int id)
    {
        return ReadLock(() => _charIDs.GetMultiple(id));
    }

    public List<AniDB_Character_Seiyuu> GetBySeiyuuID(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Character_Seiyuu>()
                .Where(a => a.SeiyuuID == id)
                .ToList();
        });
    }

    public override void PopulateIndexes()
    {
        _charIDs = new PocoIndex<int, AniDB_Character_Seiyuu, int>(Cache, a => a.CharID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_Character_Seiyuu entity)
    {
        return entity.AniDB_Character_SeiyuuID;
    }

    public AniDB_Character_SeiyuuRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
