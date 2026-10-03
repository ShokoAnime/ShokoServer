using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;

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
        Map(x => x.CountryOfOrigin).Nullable();
        Map(x => x.LastUpdatedAt).Nullable();
        Map(x => x.LastOrphanedAt).Nullable();
        Map(x => x.LastRefreshedAt).Nullable();
        Map(x => x.ExtraData).CustomType<JsonObjectConverter<Metadata_NetworkExtra>>().Nullable();
    }
}
