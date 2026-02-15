using FluentNHibernate.Mapping;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Shoko;

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
        Map(x => x.LastVideoUpdate);
        Map(x => x.HiddenUnwatchedEpisodeCount).Not.Nullable();
        Map(x => x.IsFavorite).Not.Nullable();
        Map(x => x.AbsoluteUserRating);
        Map(x => x.UserRatingVoteType).CustomType<SeriesVoteType>();
        Map(x => x.UserTags).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.LastUpdated).Not.Nullable();
    }
}
