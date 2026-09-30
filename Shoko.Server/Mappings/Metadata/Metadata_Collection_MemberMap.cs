using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_Collection_MemberMap : ClassMap<Metadata_Collection_Member>
{
    public Metadata_Collection_MemberMap()
    {
        Table("Metadata_Collection_Member");

        Not.LazyLoad();
        Id(x => x.Metadata_Collection_MemberID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.CollectionID).Not.Nullable();
        Map(x => x.MemberType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.MemberID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
