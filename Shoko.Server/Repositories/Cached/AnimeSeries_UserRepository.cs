using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class AnimeSeries_UserRepository : BaseCachedRepository<AnimeSeries_User, int>
{
    private PocoIndex<int, AnimeSeries_User, int>? _userIDs;

    private PocoIndex<int, AnimeSeries_User, int>? _seriesIDs;

    private PocoIndex<int, AnimeSeries_User, (int UserID, int SeriesID)>? _userSeriesIDs;

    public AnimeSeries_UserRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    protected override int SelectKey(AnimeSeries_User entity)
        => entity.AnimeSeries_UserID;

    public override void PopulateIndexes()
    {
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _seriesIDs = Cache.CreateIndex(a => a.AnimeSeriesID);
        _userSeriesIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeSeriesID));
    }

    public AnimeSeries_User? GetByUserAndSeriesID(int userID, int seriesID)
        => _userSeriesIDs!.GetOne((userID, seriesID));

    public List<AnimeSeries_User> GetByUserID(int userID)
        => _userIDs!.GetMultiple(userID);

    public List<AnimeSeries_User> GetBySeriesID(int seriesID)
        => _seriesIDs!.GetMultiple(seriesID);

    public List<AnimeSeries_User> GetMostRecentlyWatched(int userID)
        => GetByUserID(userID)
            .Where(a => a.UnwatchedEpisodeCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .ToList();
}
