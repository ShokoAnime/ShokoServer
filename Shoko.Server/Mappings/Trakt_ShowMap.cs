using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
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