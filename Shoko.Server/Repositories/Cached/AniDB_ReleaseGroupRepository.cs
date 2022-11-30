using System.Collections.Generic;
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

    public IList<string> GetAllReleaseGroups()
    {
        var query =
            @"SELECT g.GroupName
FROM AniDB_File a
INNER JOIN AniDB_ReleaseGroup g ON a.GroupID = g.GroupID
INNER JOIN CrossRef_File_Episode xref1 ON xref1.Hash = a.Hash
GROUP BY g.GroupName
ORDER BY count(DISTINCT xref1.AnimeID) DESC, g.GroupName ASC";

        IList<string> result;
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            result = session.CreateSQLQuery(query).List<string>();
        }

        if (result.Contains("raw/unknown"))
        {
            result.Remove("raw/unknown");
        }

        return result;
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
