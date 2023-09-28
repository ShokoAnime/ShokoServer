using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_Show_NetworkMap : ClassMap<TMDB_Show_Network>
{
    public TMDB_Show_NetworkMap()
    {
        Table("TMDB_Show_Network");

        Not.LazyLoad();
        Id(x => x.TMDB_Show_NetworkID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbNetworkID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
