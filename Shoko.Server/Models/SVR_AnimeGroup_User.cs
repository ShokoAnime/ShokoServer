using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_AnimeGroup_User : AnimeGroup_User
{
    public SVR_AnimeGroup_User()
    {
    }

    private static Logger logger = LogManager.GetCurrentClassLogger();

    private DateTime _lastPlexRegen = DateTime.MinValue;
    private Video _plexContract;

    public virtual Video PlexContract
    {
        get
        {
            if (_plexContract == null || _lastPlexRegen.Add(TimeSpan.FromMinutes(10)) > DateTime.Now)
            {
                _lastPlexRegen = DateTime.Now;
                var group = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
                return _plexContract = Helper.GenerateFromAnimeGroup(group, JMMUserID, group.GetAllSeries());
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


    public SVR_AnimeGroup_User(int userID, int groupID)
    {
        JMMUserID = userID;
        AnimeGroupID = groupID;
        IsFave = 0;
        UnwatchedEpisodeCount = 0;
        WatchedEpisodeCount = 0;
        WatchedDate = null;
        PlayedCount = 0;
        WatchedCount = 0;
        StoppedCount = 0;
    }


    public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types)
    {
        var grp = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
        var usr = RepoFactory.JMMUser.GetByID(JMMUserID);
        if (grp != null && usr != null)
        {
            grp.UpdateGroupFilters(types, usr);
        }
    }

    public void DeleteFromFilters()
    {
        var toSave = RepoFactory.GroupFilter.GetAll().AsParallel()
            .Where(gf => gf.DeleteGroupFromFilters(JMMUserID, AnimeGroupID)).ToList();
        RepoFactory.GroupFilter.Save(toSave);
    }

    public void UpdatePlexKodiContracts()
    {
        var grp = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
        if (grp == null)
        {
            return;
        }

        var series = grp.GetAllSeries();
        PlexContract = Helper.GenerateFromAnimeGroup(grp, JMMUserID, series);
    }
}
