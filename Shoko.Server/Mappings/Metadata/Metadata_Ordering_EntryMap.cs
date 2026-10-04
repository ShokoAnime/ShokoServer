using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_Ordering_EntryMap : ClassMap<Metadata_Ordering_Entry>
{
    public Metadata_Ordering_EntryMap()
    {
        Table("Metadata_Ordering_Entry");

        Not.LazyLoad();
        Id(x => x.Metadata_Ordering_EntryID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.OrderingID).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.GroupID).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.Position).Not.Nullable();
        Map(x => x.EpisodeSource).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EpisodeID).CustomType<PooledStringType>().Not.Nullable();
    }
}
