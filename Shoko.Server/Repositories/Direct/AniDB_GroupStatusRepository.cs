using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
{
    private readonly JobFactory _jobFactory;

    public List<AniDB_GroupStatus> GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_GroupStatus>()
                .Where(a => a.AnimeID == id)
                .ToList();
        });
    }

    public void DeleteForAnime(int animeid)
    {
        Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            // Query can't batch delete, while Query can
            session.Query<AniDB_GroupStatus>().Where(a => a.AnimeID == animeid).Delete();
        });

        _jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = animeid).Process().GetAwaiter().GetResult();
    }

    public AniDB_GroupStatusRepository(DatabaseFactory databaseFactory, JobFactory jobFactory) : base(databaseFactory)
    {
        _jobFactory = jobFactory;
    }
}
