using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisode_UserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_AnimeEpisode_User, int>(databaseFactory)
{
    private PocoIndex<int, SVR_AnimeEpisode_User, int>? _userIDs;

    private PocoIndex<int, SVR_AnimeEpisode_User, int>? _episodeIDs;

    private PocoIndex<int, SVR_AnimeEpisode_User, (int UserID, int EpisodeID)>? _userEpisodeIDs;

    private PocoIndex<int, SVR_AnimeEpisode_User, (int UserID, int SeriesID)>? _userSeriesIDs;

    protected override int SelectKey(SVR_AnimeEpisode_User entity)
        => entity.AnimeEpisode_UserID;

    public override void PopulateIndexes()
    {
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _episodeIDs = Cache.CreateIndex(a => a.AnimeEpisodeID);
        _userEpisodeIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeEpisodeID));
        _userSeriesIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeSeriesID));
    }

    public override void RegenerateDb()
    {
        var current = 0;
        var records = Cache.Values.Where(a => a.AnimeEpisode_UserID == 0).ToList();
        var total = records.Count;
        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(AnimeEpisode_User)} Database Regeneration...";
        if (total is 0)
            return;

        foreach (var record in records)
        {
            Save(record);
            current++;
            if (current % 10 == 0)
                ServerState.Instance.ServerStartingStatus =
                    $"Database - Validating - {nameof(AnimeEpisode_User)} Database Regeneration - {current}/{total}...";
        }

        ServerState.Instance.ServerStartingStatus =
            $"Database - Validating - {nameof(AnimeEpisode_User)} Database Regeneration - {total}/{total}...";
    }

    public SVR_AnimeEpisode_User? GetByUserIDAndEpisodeID(int userID, int episodeID)
        => ReadLock(() => _userEpisodeIDs!.GetOne((userID, episodeID)));

    public IReadOnlyList<SVR_AnimeEpisode_User> GetByUserID(int userid)
        => ReadLock(() => _userIDs!.GetMultiple(userid));

    public IReadOnlyList<SVR_AnimeEpisode_User> GetMostRecentlyWatched(int userid, int limit = 100)
        => GetByUserID(userid).Where(a => a.WatchedCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .Take(limit)
            .ToList();

    public SVR_AnimeEpisode_User? GetLastWatchedEpisodeForSeries(int seriesID, int userID)
        => GetByUserIDAndSeriesID(userID, seriesID)
            .Where(a => a.WatchedCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .FirstOrDefault();

    public IReadOnlyList<SVR_AnimeEpisode_User> GetByEpisodeID(int episodeID)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeID));

    public IReadOnlyList<SVR_AnimeEpisode_User> GetByUserIDAndSeriesID(int userID, int seriesID)
        => ReadLock(() => _userSeriesIDs!.GetMultiple((userID, seriesID)));
}
