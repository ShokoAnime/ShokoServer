using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_Network_EntryMap : ClassMap<Metadata_Network_Entry>
{
    public Metadata_Network_EntryMap()
    {
        Table("Metadata_Network_Entry");

        Not.LazyLoad();
        Id(x => x.Metadata_Network_EntryID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).Not.Nullable();
        Map(x => x.NetworkID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
