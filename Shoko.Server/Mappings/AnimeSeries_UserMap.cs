﻿using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings;

public class AnimeSeries_UserMap : ClassMap<AnimeSeries_User>
{
    public AnimeSeries_UserMap()
    {
        Table("AnimeSeries_User");

        Not.LazyLoad();
        Id(x => x.AnimeSeries_UserID);
        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.AnimeSeriesID).Not.Nullable();
        Map(x => x.PlayedCount).Not.Nullable();
        Map(x => x.StoppedCount).Not.Nullable();
        Map(x => x.UnwatchedEpisodeCount).Not.Nullable();
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.WatchedEpisodeCount).Not.Nullable();
        Map(x => x.LastEpisodeUpdate);
        Map(x => x.HiddenUnwatchedEpisodeCount).Not.Nullable();
    }
}
