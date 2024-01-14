using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
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

    public IReadOnlyList<AniDB_ReleaseGroup> GetUsedReleaseGroups()
    {
        var results = Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_ReleaseGroup>().Where(a => a.GroupName != "raw/unknown").Join(session.Query<AniDB_File>(), a => a.GroupID, a => a.GroupID, (a, b) => new { Group = a, File = b })
                .Join(session.Query<CrossRef_File_Episode>(), a => a.File.Hash, a => a.Hash, (a, b) => a.Group).OrderBy(a => a.GroupName).ToList();
        });
        return results;
    }

    public IReadOnlyList<AniDB_ReleaseGroup> GetUnusedReleaseGroups()
    {
        var results = Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_ReleaseGroup>().Where(a => a.GroupName != "raw/unknown").LeftJoin(session.Query<AniDB_File>(), a => a.GroupID, a => a.GroupID,
                (a, b) => new { Group = a, File = b }).Where(a => a.File == null).Select(a => a.Group).OrderBy(a => a.GroupName).ToList();
        });
        return results;
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
