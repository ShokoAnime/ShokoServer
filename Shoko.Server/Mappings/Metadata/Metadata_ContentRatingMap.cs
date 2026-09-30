using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_ContentRatingMap : ClassMap<Metadata_ContentRating>
{
    public Metadata_ContentRatingMap()
    {
        Table("Metadata_ContentRating");

        Not.LazyLoad();
        Id(x => x.Metadata_ContentRatingID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).Not.Nullable();
        Map(x => x.CountryCode).Not.Nullable();
        Map(x => x.LanguageCode).Not.Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
