using System.Collections.Generic;
using System.Linq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_GroupStatusRepository : BaseCachedRepository<AniDB_GroupStatus, int>
{
    private PocoIndex<int, AniDB_GroupStatus, int>? _animeIDs;

    private readonly IQueueScheduler _scheduler;

    protected override int SelectKey(AniDB_GroupStatus entity)
        => entity.AniDB_GroupStatusID;

    public override void PopulateIndexes()
        => _animeIDs = Cache.CreateIndex(a => a.AnimeID);

    public virtual List<AniDB_GroupStatus> GetByAnimeID(int id)
        => _animeIDs!.GetMultiple(id);

    /// <summary>
    /// Gets the cached group release statuses for multiple anime in a single batched lookup.
    /// </summary>
    /// <param name="animeIDs">The AniDB anime IDs to look up. Duplicates are ignored.</param>
    /// <returns>A dictionary keyed by anime ID containing the group statuses for that anime. Anime without any statuses are omitted from the result.</returns>
    public Dictionary<int, List<AniDB_GroupStatus>> GetByAnimeIDs(IEnumerable<int> animeIDs)
    {
        var result = new Dictionary<int, List<AniDB_GroupStatus>>();
        foreach (var id in animeIDs.Distinct())
        {
            var statuses = _animeIDs!.GetMultiple(id);
            if (statuses.Count > 0)
                result[id] = statuses;
        }

        return result;
    }

    public void DeleteForAnime(int animeid)
    {
        Delete(_animeIDs!.GetMultiple(animeid));

        _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = animeid).GetAwaiter().GetResult();
    }

    public AniDB_GroupStatusRepository(DatabaseFactory databaseFactory, IQueueScheduler scheduler) : base(databaseFactory)
    {
        _scheduler = scheduler;
    }
}
