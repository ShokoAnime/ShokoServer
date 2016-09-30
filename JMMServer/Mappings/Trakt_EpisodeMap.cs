using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class Trakt_EpisodeMap : ClassMap<Trakt_Episode>
    {
        public Trakt_EpisodeMap()
        {
            Not.LazyLoad();
            Id(x => x.Trakt_EpisodeID);

            Map(x => x.Trakt_ShowID).Not.Nullable();
            Map(x => x.EpisodeImage);
            Map(x => x.EpisodeNumber);
            Map(x => x.Overview).CustomType("StringClob"); ;
            Map(x => x.Season).Not.Nullable();
            Map(x => x.Title);
            Map(x => x.URL);
            Map(x => x.TraktID);
        }
    }
}