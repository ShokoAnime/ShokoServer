using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
{
    private readonly IQueueScheduler _scheduler;

    public List<AniDB_GroupStatus> GetByAnimeID(int id)
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<AniDB_GroupStatus>()
            .Where(a => a.AnimeID == id)
            .ToList();
    }

    /// <summary>
    /// Gets the cached group release statuses for multiple anime in a single batched query.
    /// </summary>
    /// <param name="animeIDs">The AniDB anime IDs to look up. Duplicates are ignored.</param>
    /// <returns>A dictionary keyed by anime ID containing the group statuses for that anime. Anime without any statuses are omitted from the result.</returns>
    public Dictionary<int, List<AniDB_GroupStatus>> GetByAnimeIDs(IEnumerable<int> animeIDs)
    {
        var idList = animeIDs.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<int, List<AniDB_GroupStatus>>();

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        var records = idList
            .Batch(1000)
            .SelectMany(batch => session.Query<AniDB_GroupStatus>()
                .Where(a => batch.Contains(a.AnimeID))
                .ToList())
            .ToList();

        return records
            .GroupBy(a => a.AnimeID)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public void DeleteForAnime(int animeid)
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        session.Query<AniDB_GroupStatus>().Where(a => a.AnimeID == animeid).Delete();

        _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeid).GetAwaiter().GetResult();
    }

    public AniDB_GroupStatusRepository(DatabaseFactory databaseFactory, IQueueScheduler scheduler) : base(databaseFactory)
    {
        _scheduler = scheduler;
    }
}
