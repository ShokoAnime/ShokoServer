using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Scheduling;

namespace Shoko.TestData.Repositories;

/// <summary>
/// Mock AniDB_Anime Repository. Many methods will error, such as write operations
/// </summary>
public class AniDB_FileRepository : Server.Repositories.Cached.AniDB.AniDB_FileRepository
{
    public override void RegenerateDb()
    {
        // noop
    }

    public override void Populate(ISessionWrapper session, bool displayname = true)
    {
        Cache = new PocoCache<int, SVR_AniDB_File>(TestData.AniDB_File.Value, SelectKey);
        PopulateIndexes();
    }

    public override void Populate(bool displayname = true)
    {
        Populate(null!, displayname);
    }

    public AniDB_FileRepository(ILogger<AniDB_FileRepository> logger, DatabaseFactory databaseFactory, JobFactory jobFactory) : base(logger, jobFactory, databaseFactory) { }
}
