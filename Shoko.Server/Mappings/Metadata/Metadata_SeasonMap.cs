using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;

namespace Shoko.Server.Mappings;

public class Metadata_SeasonMap : ClassMap<Metadata_Season>
{
    public Metadata_SeasonMap()
    {
        Table("Metadata_Season");

        Not.LazyLoad();
        Id(x => x.Metadata_SeasonID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.SeriesID).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.ExtraData).CustomType<JsonObjectConverter<Metadata_SeasonExtra>>().Nullable();
    }
}
