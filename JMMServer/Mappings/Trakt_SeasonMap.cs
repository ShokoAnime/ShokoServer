using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class Trakt_SeasonMap : ClassMap<Trakt_Season>
    {
        public Trakt_SeasonMap()
        {
            Not.LazyLoad();
            Id(x => x.Trakt_SeasonID);

            Map(x => x.Season).Not.Nullable();
            Map(x => x.Trakt_ShowID).Not.Nullable();
            Map(x => x.URL);
        }
    }
}