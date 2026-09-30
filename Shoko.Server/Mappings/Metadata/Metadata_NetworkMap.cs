using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_NetworkMap : ClassMap<Metadata_Network>
{
    public Metadata_NetworkMap()
    {
        Table("Metadata_Network");

        Not.LazyLoad();
        Id(x => x.Metadata_NetworkID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.LastOrphanedAt).Nullable();
    }
}
