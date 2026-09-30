using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_SeriesMap : ClassMap<Metadata_Series>
{
    public Metadata_SeriesMap()
    {
        Table("Metadata_Series");

        Not.LazyLoad();
        Id(x => x.Metadata_SeriesID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Type).CustomType<AnimeType>().Not.Nullable();
        Map(x => x.AirDate).CustomType<PartialDateOnlyConverter>().Nullable();
        Map(x => x.EndDate).CustomType<PartialDateOnlyConverter>().Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.RatingVotes).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.ReleaseStatus).CustomType<ReleaseStatus>().Not.Nullable();
        Map(x => x.SourceMaterial).CustomType<SourceMaterial>().Not.Nullable();
        Map(x => x.OriginalLanguageCode).Nullable();
        Map(x => x.Popularity).Nullable();
        Map(x => x.FavoriteCount).Nullable();
        Map(x => x.Resources).CustomType<JsonListConverter<Resource>>().Nullable();
        Map(x => x.CrossSourceIDs).CustomType<JsonListConverter<MetadataGuid>>().Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.PreferredOrderingID).CustomType<MetadataGuidType>().Nullable();
    }
}
