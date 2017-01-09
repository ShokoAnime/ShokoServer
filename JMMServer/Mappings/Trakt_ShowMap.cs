using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class Trakt_ShowMap : ClassMap<Trakt_Show>
    {
        public Trakt_ShowMap()
        {
            Not.LazyLoad();
            Id(x => x.Trakt_ShowID);

            Map(x => x.Overview);
            Map(x => x.Title);
            Map(x => x.TraktID);
            Map(x => x.TvDB_ID);
            Map(x => x.URL);
            Map(x => x.Year);
        }
    }
}