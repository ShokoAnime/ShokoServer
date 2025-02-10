#nullable enable
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_CreatorRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Creator, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Creator, int>? _creatorIDs;

    protected override int SelectKey(AniDB_Creator entity)
        => entity.AniDB_CreatorID;

    public override void PopulateIndexes()
    {
        _creatorIDs = Cache.CreateIndex(a => a.CreatorID);
    }

    public AniDB_Creator? GetByCreatorID(int creatorID)
    {
        return ReadLock(() => _creatorIDs!.GetOne(creatorID));
    }

    public AniDB_Creator? GetByName(string creatorName)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<AniDB_Creator>()
                .Where(a => a.Name == creatorName)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
