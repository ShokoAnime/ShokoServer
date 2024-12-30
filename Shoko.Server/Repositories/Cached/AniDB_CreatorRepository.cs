using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Databases;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AniDB_CreatorRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Creator, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Creator, int>? _creatorIDs;

    protected override int SelectKey(AniDB_Creator entity)
        => entity.AniDB_CreatorID;

    public override void PopulateIndexes()
    {
        _creatorIDs = new PocoIndex<int, AniDB_Creator, int>(Cache, a => a.CreatorID);
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
