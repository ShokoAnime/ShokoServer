using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_RefreshMap : ClassMap<Metadata_Refresh>
{
    public Metadata_RefreshMap()
    {
        Table("Metadata_Refresh");

        Not.LazyLoad();
        Id(x => x.Metadata_RefreshID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.LastRefreshedAt).Not.Nullable();
    }
}
