using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Databases;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AniDB_CreatorRepository : BaseCachedRepository<AniDB_Creator, int>
{
    private PocoIndex<int, AniDB_Creator, int>? _seiyuuIDs;

    public AniDB_Creator? GetByCreatorID(int id)
    {
        return ReadLock(() => _seiyuuIDs!.GetOne(id));
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

    public override void PopulateIndexes()
    {
        _seiyuuIDs = new PocoIndex<int, AniDB_Creator, int>(Cache, a => a.CreatorID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_Creator entity)
    {
        return entity.AniDB_CreatorID;
    }

    public AniDB_CreatorRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
