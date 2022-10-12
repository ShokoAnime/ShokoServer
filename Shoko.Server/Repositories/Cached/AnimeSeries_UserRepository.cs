﻿using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models.Client;
using Shoko.Models.Enums;
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
            if (!Changes.ContainsKey(cr.JMMUserID))
            {
                Changes[cr.JMMUserID] = new ChangeTracker<int>();
            }

            Changes[cr.JMMUserID].Remove(cr.AnimeSeriesID);

            cr.DeleteFromFilters();
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
        SVR_AnimeSeries_User old;
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            old = session.Get<SVR_AnimeSeries_User>(obj.AnimeSeries_UserID);
        }

        var types = SVR_AnimeSeries_User.GetConditionTypesChanged(old, obj);
        base.Save(obj);
        if (!Changes.ContainsKey(obj.JMMUserID))
        {
            Changes[obj.JMMUserID] = new ChangeTracker<int>();
        }

        Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);

        obj.UpdateGroupFilter(types);
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
        return Changes.ContainsKey(userid) ? Changes[userid] : new ChangeTracker<int>();
    }
}
