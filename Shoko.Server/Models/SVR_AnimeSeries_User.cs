﻿using System;
using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_AnimeSeries_User : AnimeSeries_User
{
    public SVR_AnimeSeries_User()
    {
    }

    public SVR_AnimeSeries_User(int userID, int seriesID)
    {
        JMMUserID = userID;
        AnimeSeriesID = seriesID;
        UnwatchedEpisodeCount = 0;
        WatchedEpisodeCount = 0;
        WatchedDate = null;
        PlayedCount = 0;
        WatchedCount = 0;
        StoppedCount = 0;
        LastEpisodeUpdate = null;
    }

    public virtual SVR_AnimeSeries AnimeSeries
        => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    private DateTime _lastPlexRegen = DateTime.MinValue;
    private Video _plexContract;

    public virtual Video PlexContract
    {
        get
        {
            if (_plexContract == null || _lastPlexRegen.Add(TimeSpan.FromMinutes(10)) > DateTime.Now)
            {
                _lastPlexRegen = DateTime.Now;
                var series = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
                return _plexContract = Helper.GenerateFromSeries(series.GetUserContract(JMMUserID), series,
                    series.GetAnime(), JMMUserID);
            }

            return _plexContract;
        }
        set
        {
            _plexContract = value;
            _lastPlexRegen = DateTime.Now;
        }
    }

    public void CollectContractMemory()
    {
        _plexContract = null;
    }

    public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(SVR_AnimeSeries_User oldcontract,
        SVR_AnimeSeries_User newcontract)
    {
        var h = new HashSet<GroupFilterConditionType>();

        if (oldcontract == null ||
            oldcontract.UnwatchedEpisodeCount > 0 != newcontract.UnwatchedEpisodeCount > 0)
        {
            h.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
        }

        if (oldcontract == null || oldcontract.WatchedDate != newcontract.WatchedDate)
        {
            h.Add(GroupFilterConditionType.EpisodeWatchedDate);
        }

        if (oldcontract == null || oldcontract.WatchedEpisodeCount > 0 != newcontract.WatchedEpisodeCount > 0)
        {
            h.Add(GroupFilterConditionType.HasWatchedEpisodes);
        }

        return h;
    }

    public void UpdateGroupFilter(HashSet<GroupFilterConditionType> types)
    {
        var ser = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
        var usr = RepoFactory.JMMUser.GetByID(JMMUserID);
        if (ser != null && usr != null)
        {
            ser.UpdateGroupFilters(types, usr);
        }
    }

    public void DeleteFromFilters()
    {
        foreach (var gf in RepoFactory.GroupFilter.GetAll())
        {
            var change = false;
            if (gf.SeriesIds.ContainsKey(JMMUserID))
            {
                if (gf.SeriesIds[JMMUserID].Contains(AnimeSeriesID))
                {
                    gf.SeriesIds[JMMUserID].Remove(AnimeSeriesID);
                    change = true;
                }
            }

            if (change)
            {
                RepoFactory.GroupFilter.Save(gf);
            }
        }
    }
}
