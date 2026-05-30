using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
{
    private readonly IQueueScheduler _scheduler;

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
            session.Query<AniDB_GroupStatus>().Where(a => a.AnimeID == animeid).Delete();
        });

        _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeid).GetAwaiter().GetResult();
    }

    public AniDB_GroupStatusRepository(DatabaseFactory databaseFactory, IQueueScheduler scheduler) : base(databaseFactory)
    {
        _scheduler = scheduler;
    }
}
