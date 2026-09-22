using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisode_UserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AnimeEpisode_User, int>(databaseFactory)
{
    private PocoIndex<int, AnimeEpisode_User, int>? _userIDs;

    private PocoIndex<int, AnimeEpisode_User, int>? _episodeIDs;

    private PocoIndex<int, AnimeEpisode_User, (int UserID, int EpisodeID)>? _userEpisodeIDs;

    protected override int SelectKey(AnimeEpisode_User entity)
        => entity.AnimeEpisode_UserID;

    public override void PopulateIndexes()
    {
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _episodeIDs = Cache.CreateIndex(a => a.AnimeEpisodeID);
        _userEpisodeIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeEpisodeID));
    }

    public override void RegenerateDb()
    {
        var current = 0;
        var records = Cache.GetAll().Where(a => a.AnimeEpisode_UserID == 0).ToList();
        var total = records.Count;
        SystemService.StartupMessage = $"Database - Validating - {nameof(AnimeEpisode_User)} Database Regeneration...";
        if (total is 0)
            return;

        foreach (var record in records)
        {
            Save(record);
            current++;
            if (current % 10 == 0)
                SystemService.StartupMessage =
                    $"Database - Validating - {nameof(AnimeEpisode_User)} Database Regeneration - {current}/{total}...";
        }

        SystemService.StartupMessage =
            $"Database - Validating - {nameof(AnimeEpisode_User)} Database Regeneration - {total}/{total}...";
    }

    public AnimeEpisode_User? GetByUserAndEpisodeID(int userID, int episodeID)
        => _userEpisodeIDs!.GetOne((userID, episodeID));

    public IReadOnlyList<AnimeEpisode_User> GetByUserID(int userid)
        => _userIDs!.GetMultiple(userid);

    public IReadOnlyList<AnimeEpisode_User> GetMostRecentlyWatched(int userid, int limit = 100)
        => GetByUserID(userid).Where(a => a.WatchedCount > 0)
            .OrderByDescending(a => a.WatchedDate)
            .Take(limit)
            .ToList();

    public IReadOnlyList<AnimeEpisode_User> GetByEpisodeID(int episodeID)
        => _episodeIDs!.GetMultiple(episodeID);
}
