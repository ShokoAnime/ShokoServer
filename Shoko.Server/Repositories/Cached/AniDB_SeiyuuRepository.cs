using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_SeiyuuRepository : BaseCachedRepository<AniDB_Seiyuu, int>
{
    private PocoIndex<int, AniDB_Seiyuu, int> _seiyuuIDs;

    public AniDB_Seiyuu GetBySeiyuuID(int id)
    {
        return ReadLock(() => _seiyuuIDs.GetOne(id));
    }

    public override void PopulateIndexes()
    {
        _seiyuuIDs = new PocoIndex<int, AniDB_Seiyuu, int>(Cache, a => a.SeiyuuID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_Seiyuu entity)
    {
        return entity.SeiyuuID;
    }

    public AniDB_SeiyuuRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
