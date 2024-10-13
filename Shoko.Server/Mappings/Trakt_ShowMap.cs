using FluentNHibernate.Mapping;
using Shoko.Server.Models.Trakt;

namespace Shoko.Server.Mappings;

public class Trakt_ShowMap : ClassMap<Trakt_Show>
{
    public Trakt_ShowMap()
    {
        Not.LazyLoad();
        Id(x => x.Trakt_ShowID);

        Map(x => x.TraktID);
        Map(x => x.TmdbShowID);
        Map(x => x.Title);
        Map(x => x.Year);
        Map(x => x.URL);
        Map(x => x.Overview);
    }
}
