using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_OrderingMap : ClassMap<Metadata_Ordering>
{
    public Metadata_OrderingMap()
    {
        Table("Metadata_Ordering");

        Not.LazyLoad();
        Id(x => x.Metadata_OrderingID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.SeriesSource).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.SeriesID).Not.Nullable();
        Map(x => x.Type).CustomType<OrderingType>().Not.Nullable();
        Map(x => x.Name).Not.Nullable().CustomType("StringClob");
        Map(x => x.Description).Nullable().CustomType("StringClob");
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
