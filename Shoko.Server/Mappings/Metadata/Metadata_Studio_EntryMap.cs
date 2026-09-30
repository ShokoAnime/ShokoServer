using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_Studio_EntryMap : ClassMap<Metadata_Studio_Entry>
{
    public Metadata_Studio_EntryMap()
    {
        Table("Metadata_Studio_Entry");

        Not.LazyLoad();
        Id(x => x.Metadata_Studio_EntryID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).Not.Nullable();
        Map(x => x.StudioID).Not.Nullable();
        Map(x => x.StudioType).CustomType<StudioType>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
