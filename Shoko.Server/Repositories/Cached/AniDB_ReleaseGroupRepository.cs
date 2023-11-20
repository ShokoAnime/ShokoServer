using System.Collections.Generic;
using System.Linq;
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

    const string UsedReleaseGroupsQuery = @"SELECT {g.*}
FROM AniDB_File f
INNER JOIN AniDB_ReleaseGroup g ON f.GroupID = g.GroupID
INNER JOIN CrossRef_File_Episode x ON x.Hash = f.Hash
GROUP BY g.GroupID
ORDER BY g.GroupName ASC";

    public IReadOnlyList<AniDB_ReleaseGroup> GetUsedReleaseGroups()
    {
        var results = Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateSQLQuery(UsedReleaseGroupsQuery).AddEntity("g", typeof(AniDB_ReleaseGroup))
                .List<object>();
        });
        return results
            .Select(result => (AniDB_ReleaseGroup)result)
            .Where(result => !string.Equals(result.GroupName, "raw/unknown", System.StringComparison.InvariantCultureIgnoreCase))
            .ToList();
    }

    const string UnusedReleaseGroupsQuery = @"SELECT {g.*}
FROM AniDB_ReleaseGroup g
LEFT JOIN AniDB_File f ON f.GroupID = g.GroupID
WHERE f.GroupID IS NULL
GROUP BY g.GroupID
ORDER BY g.GroupName ASC";

    public IReadOnlyList<AniDB_ReleaseGroup> GetUnusedReleaseGroups()
    {
        var results = Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateSQLQuery(UnusedReleaseGroupsQuery).AddEntity("g", typeof(AniDB_ReleaseGroup))
                .List<object>();
        });
        return results
            .Select(result => (AniDB_ReleaseGroup)result)
            .Where(result => !string.Equals(result.GroupName, "raw/unknown", System.StringComparison.InvariantCultureIgnoreCase))
            .ToList();
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
