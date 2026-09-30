using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_CollectionMap : ClassMap<Metadata_Collection>
{
    public Metadata_CollectionMap()
    {
        Table("Metadata_Collection");

        Not.LazyLoad();
        Id(x => x.Metadata_CollectionID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
