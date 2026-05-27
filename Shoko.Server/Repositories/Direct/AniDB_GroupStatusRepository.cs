using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
{
    private readonly IServiceProvider _serviceProvider;

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

        var job = _serviceProvider.GetRequiredService<RefreshAnimeStatsJob>();
        job.AnimeID = animeid;
        job.Process().GetAwaiter().GetResult();
    }

    public AniDB_GroupStatusRepository(DatabaseFactory databaseFactory, IServiceProvider serviceProvider) : base(databaseFactory)
    {
        _serviceProvider = serviceProvider;
    }
}
