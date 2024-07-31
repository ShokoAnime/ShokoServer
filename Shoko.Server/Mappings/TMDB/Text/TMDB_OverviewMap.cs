using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_OverviewMap : ClassMap<TMDB_Overview>
{
    public TMDB_OverviewMap()
    {
        Table("TMDB_Overview");

        Not.LazyLoad();
        Id(x => x.TMDB_OverviewID);

        Map(x => x.ParentID).Not.Nullable();
        Map(x => x.ParentType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.LanguageCode).Not.Nullable();
        Map(x => x.CountryCode).Not.Nullable();
        Map(x => x.Value).Not.Nullable();
    }
}
