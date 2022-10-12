﻿using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class AnimeGroup_UserMap : ClassMap<SVR_AnimeGroup_User>
{
    public AnimeGroup_UserMap()
    {
        Table("AnimeGroup_User");

        Not.LazyLoad();
        Id(x => x.AnimeGroup_UserID);
        Map(x => x.JMMUserID);
        Map(x => x.AnimeGroupID);
        Map(x => x.IsFave).Not.Nullable();
        Map(x => x.PlayedCount).Not.Nullable();
        Map(x => x.StoppedCount).Not.Nullable();
        Map(x => x.UnwatchedEpisodeCount).Not.Nullable();
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.WatchedEpisodeCount);
    }
}
