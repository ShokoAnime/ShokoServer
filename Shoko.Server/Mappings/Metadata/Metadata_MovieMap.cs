using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_MovieMap : ClassMap<Metadata_Movie>
{
    public Metadata_MovieMap()
    {
        Table("Metadata_Movie");

        Not.LazyLoad();
        Id(x => x.Metadata_MovieID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.ReleasedAt).CustomType<DateOnlyConverter>().Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.IsVideo).Not.Nullable();
        Map(x => x.OriginalLanguageCode).Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.RatingVotes).Not.Nullable();
        Map(x => x.Resources).CustomType<JsonListConverter<Resource>>().Nullable();
        Map(x => x.CrossSourceIDs).CustomType<JsonListConverter<MetadataGuid>>().Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
