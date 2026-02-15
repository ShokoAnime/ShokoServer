using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class AnimeEpisode_UserMap : ClassMap<AnimeEpisode_User>
{
    public AnimeEpisode_UserMap()
    {
        Table("AnimeEpisode_User");
        Not.LazyLoad();
        Id(x => x.AnimeEpisode_UserID);

        Map(x => x.AnimeEpisodeID).Not.Nullable();
        Map(x => x.AnimeSeriesID).Not.Nullable();
        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.PlayedCount).Not.Nullable();
        Map(x => x.StoppedCount).Not.Nullable();
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.IsFavorite).Not.Nullable();
        Map(x => x.AbsoluteUserRating);
        Map(x => x.UserTags).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.LastUpdated).Not.Nullable();
    }
}
