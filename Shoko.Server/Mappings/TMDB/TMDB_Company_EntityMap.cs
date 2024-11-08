using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_Company_EntityMap : ClassMap<TMDB_Company_Entity>
{
    public TMDB_Company_EntityMap()
    {
        Table("TMDB_Company_Entity");

        Not.LazyLoad();
        Id(x => x.TMDB_Company_EntityID);

        Map(x => x.TmdbCompanyID).Not.Nullable();
        Map(x => x.TmdbEntityType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.TmdbEntityID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.ReleasedAt).CustomType<DateOnlyConverter>();
    }
}
