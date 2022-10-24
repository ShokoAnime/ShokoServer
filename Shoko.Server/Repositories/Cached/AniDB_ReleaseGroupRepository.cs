using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_ReleaseGroupRepository : BaseCachedRepository<AniDB_ReleaseGroup, int>
{
    private PocoIndex<int, AniDB_ReleaseGroup, int> GroupIDs;

    public AniDB_ReleaseGroup GetByGroupID(int id)
    {
        return ReadLock(() => GroupIDs.GetOne(id));
    }

    public override void PopulateIndexes()
    {
        GroupIDs = Cache.CreateIndex(a => a.GroupID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_ReleaseGroup entity)
    {
        return entity.AniDB_ReleaseGroupID;
    }
}
