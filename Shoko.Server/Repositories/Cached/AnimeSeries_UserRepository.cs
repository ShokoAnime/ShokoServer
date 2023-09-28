using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.Repositories.Cached;

public class AnimeSeries_UserRepository : BaseCachedRepository<SVR_AnimeSeries_User, int>
{
    private PocoIndex<int, SVR_AnimeSeries_User, int> Users;
    private PocoIndex<int, SVR_AnimeSeries_User, int> Series;
    private PocoIndex<int, SVR_AnimeSeries_User, (int UserID, int SeriesID)> UsersSeries;
    private Dictionary<int, ChangeTracker<int>> Changes = new();

    public AnimeSeries_UserRepository()
    {
        EndDeleteCallback = cr =>
        {
            Changes.TryAdd(cr.JMMUserID, new ChangeTracker<int>());

            Changes[cr.JMMUserID].Remove(cr.AnimeSeriesID);
        };
    }

    protected override int SelectKey(SVR_AnimeSeries_User entity)
    {
        return entity.AnimeSeries_UserID;
    }

    public override void PopulateIndexes()
    {
        Users = Cache.CreateIndex(a => a.JMMUserID);
        Series = Cache.CreateIndex(a => a.AnimeSeriesID);
        UsersSeries = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeSeriesID));
    }

    public override void RegenerateDb()
    {
    }


    public override void Save(SVR_AnimeSeries_User obj)
    {
        UpdatePlexKodiContracts(obj);
        base.Save(obj);
        Changes.TryAdd(obj.JMMUserID, new ChangeTracker<int>());
        Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);
    }

    private void UpdatePlexKodiContracts(SVR_AnimeSeries_User ugrp)
    {
        var ser = RepoFactory.AnimeSeries.GetByID(ugrp.AnimeSeriesID);
        var con = ser?.GetUserContract(ugrp.JMMUserID);
        if (con == null)
        {
            return;
        }

        ugrp.PlexContract = Helper.GenerateFromSeries(con, ser, ser.GetAnime(), ugrp.JMMUserID);
    }


    public SVR_AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
    {
        return ReadLock(() => UsersSeries.GetOne((userid, seriesid)));
    }

    public List<SVR_AnimeSeries_User> GetByUserID(int userid)
    {
        return ReadLock(() => Users.GetMultiple(userid));
    }

    public List<SVR_AnimeSeries_User> GetBySeriesID(int seriesid)
    {
        return ReadLock(() => Series.GetMultiple(seriesid));
    }


    public List<SVR_AnimeSeries_User> GetMostRecentlyWatched(int userID)
    {
        return
            GetByUserID(userID)
                .Where(a => a.UnwatchedEpisodeCount > 0)
                .OrderByDescending(a => a.WatchedDate)
                .ToList();
    }


    public ChangeTracker<int> GetChangeTracker(int userid)
    {
        return Changes.TryGetValue(userid, out var change) ? change : new ChangeTracker<int>();
    }
}
