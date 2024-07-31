using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_NetworkMap : ClassMap<TMDB_Network>
{
    public TMDB_NetworkMap()
    {
        Table("TMDB_Network");

        Not.LazyLoad();
        Id(x => x.TMDB_NetworkID);

        Map(x => x.TmdbNetworkID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.CountryOfOrigin).Not.Nullable();
    }
}
