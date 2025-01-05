using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.TestData.Repositories;

/// <summary>
/// Mock AniDB_Anime Repository. Many methods will error, such as write operations
/// </summary>
public class AniDB_AnimeRepository : Server.Repositories.Cached.AniDB.AniDB_AnimeRepository
{
    public override void RegenerateDb()
    {
        // noop
    }

    public override void Populate(ISessionWrapper session, bool displayname = true)
    {
        Cache = new PocoCache<int, SVR_AniDB_Anime>(TestData.AniDB_Anime.Value, SelectKey);
        PopulateIndexes();
    }

    public override void Populate(bool displayname = true)
    {
        Populate(null!, displayname);
    }

    public AniDB_AnimeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
