using FluentNHibernate.Mapping;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_Image_EntityMap : ClassMap<TMDB_Image_Entity>
{
    public TMDB_Image_EntityMap()
    {
        Table("TMDB_Image_Entity");

        Not.LazyLoad();
        Id(x => x.TMDB_Image_EntityID);

        Map(x => x.RemoteFileName).Not.Nullable();
        Map(x => x.ImageType).Not.Nullable().CustomType<ImageEntityType>();
        Map(x => x.TmdbEntityType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.TmdbEntityID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.ReleasedAt).CustomType<DateOnlyConverter>();
    }
}
