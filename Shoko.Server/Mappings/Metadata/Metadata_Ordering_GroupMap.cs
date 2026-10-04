using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_Ordering_GroupMap : ClassMap<Metadata_Ordering_Group>
{
    public Metadata_Ordering_GroupMap()
    {
        Table("Metadata_Ordering_Group");

        Not.LazyLoad();
        Id(x => x.Metadata_Ordering_GroupID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.OrderingID).Not.Nullable();
        Map(x => x.Position).Not.Nullable();
        Map(x => x.IsSpecial).Not.Nullable();
        Map(x => x.SeasonNumber).Nullable();
    }
}
