using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class AnimeEpisode_UserMap : ClassMap<AnimeEpisode_User>
    {
        public AnimeEpisode_UserMap()
        {
            Not.LazyLoad();
            Id(x => x.AnimeEpisode_UserID);

            Map(x => x.AnimeEpisodeID).Not.Nullable();
            Map(x => x.AnimeSeriesID).Not.Nullable();
            Map(x => x.JMMUserID).Not.Nullable();
            Map(x => x.PlayedCount).Not.Nullable();
            Map(x => x.StoppedCount).Not.Nullable();
            Map(x => x.WatchedCount).Not.Nullable();
            Map(x => x.WatchedDate);
        }
    }
}