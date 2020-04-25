using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AnimeEpisodeMap : ClassMap<SVR_AnimeEpisode>
    {
        public AnimeEpisodeMap()
        {
            Table("AnimeEpisode");

            Not.LazyLoad();
            Id(x => x.AnimeEpisodeID);

            Map(x => x.AniDB_EpisodeID).Not.Nullable();
            Map(x => x.AnimeSeriesID).Not.Nullable();
            Map(x => x.DateTimeCreated).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
        }
    }
}