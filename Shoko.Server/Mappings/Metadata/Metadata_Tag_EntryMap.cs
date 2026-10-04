using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_Tag_EntryMap : ClassMap<Metadata_Tag_Entry>
{
    public Metadata_Tag_EntryMap()
    {
        Table("Metadata_Tag_Entry");

        Not.LazyLoad();
        Id(x => x.Metadata_Tag_EntryID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.TagID).Not.Nullable();
        Map(x => x.Weight).Nullable();
        Map(x => x.IsSpoiler).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
