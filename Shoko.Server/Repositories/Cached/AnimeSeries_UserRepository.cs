using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeSeries_UserRepository : BaseCachedRepository<AnimeSeries_User, int>
{
    private PocoIndex<int, AnimeSeries_User, int>? _userIDs;

    private PocoIndex<int, AnimeSeries_User, int>? _seriesIDs;

    private PocoIndex<int, AnimeSeries_User, (int UserID, int SeriesID)>? _userSeriesIDs;

    private readonly Dictionary<int, ChangeTracker<int>> _changes = [];

    public AnimeSeries_UserRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        EndDeleteCallback = cr =>
        {
            _changes.TryAdd(cr.JMMUserID, new ChangeTracker<int>());

            _changes[cr.JMMUserID].Remove(cr.AnimeSeriesID);
        };
    }

    protected override int SelectKey(AnimeSeries_User entity)
        => entity.AnimeSeries_UserID;

    public override void PopulateIndexes()
    {
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _seriesIDs = Cache.CreateIndex(a => a.AnimeSeriesID);
        _userSeriesIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeSeriesID));
    }

    public override void Save(AnimeSeries_User obj)
    {
        base.Save(obj);
        _changes.TryAdd(obj.JMMUserID, new());
        _changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);
    }

    public AnimeSeries_User? GetByUserAndSeriesID(int userID, int seriesID)
        => ReadLock(() => _userSeriesIDs!.GetOne((userID, seriesID)));

    public List<AnimeSeries_User> GetByUserID(int userID)
        => ReadLock(() => _userIDs!.GetMultiple(userID));

    public List<AnimeSeries_User> GetBySeriesID(int seriesID)
        => ReadLock(() => _seriesIDs!.GetMultiple(seriesID));

    public List<AnimeSeries_User> GetMostRecentlyWatched(int userID)
        => GetByUserID(userID)
            .Where(a => a.UnwatchedEpisodeCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .ToList();

    public ChangeTracker<int> GetChangeTracker(int userID)
        => _changes.TryGetValue(userID, out var change) ? change : new ChangeTracker<int>();
}
