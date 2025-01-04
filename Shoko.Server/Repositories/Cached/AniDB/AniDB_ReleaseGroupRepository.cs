using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_ReleaseGroupRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_ReleaseGroup, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_ReleaseGroup, int>? _groupIDs;

    protected override int SelectKey(AniDB_ReleaseGroup entity)
        => entity.AniDB_ReleaseGroupID;

    public override void PopulateIndexes()
    {
        _groupIDs = Cache.CreateIndex(a => a.GroupID);
    }

    public AniDB_ReleaseGroup? GetByGroupID(int groupID)
        => ReadLock(() => _groupIDs!.GetOne(groupID));

    public IReadOnlyList<AniDB_ReleaseGroup> GetUsedReleaseGroups()
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_ReleaseGroup>()
                .Where(a => a.GroupName != "raw/unknown")
                .Join(session.Query<AniDB_File>(), a => a.GroupID, a => a.GroupID, (a, b) => new { Group = a, File = b })
                .Join(session.Query<SVR_CrossRef_File_Episode>(), a => a.File.Hash, a => a.Hash, (a, b) => a.Group)
                .OrderBy(a => a.GroupName)
                .ToList()
                .Distinct()
                .ToList();
        });

    public IReadOnlyList<AniDB_ReleaseGroup> GetUnusedReleaseGroups()
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_ReleaseGroup>()
                .Where(a => a.GroupName != "raw/unknown")
                .LeftJoin(session.Query<AniDB_File>(), a => a.GroupID, a => a.GroupID,
                (a, b) => new { Group = a, File = b })
                .Where(a => a.File == null)
                .Select(a => a.Group)
                .OrderBy(a => a.GroupName)
                .ToList()
                .Distinct()
                .ToList();
        });
}
