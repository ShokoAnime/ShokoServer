using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class ShokoImage_EntityMap : ClassMap<ShokoImage_Entity>
{
    public ShokoImage_EntityMap()
    {
        Table("ShokoImage_Entity");

        Not.LazyLoad();
        Id(x => x.ID);

        Map(x => x.ImageID).Not.Nullable();
        Map(x => x.PrimaryImageID).Not.Nullable();
        Map(x => x.ImageType).CustomType<ImageEntityType>().Not.Nullable();
        Map(x => x.ImageSource).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntitySource).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityID).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.EntitySeasonNumber).Nullable();
        Map(x => x.EntityEpisodeNumber).Nullable();
        Map(x => x.EntityReleasedAt).CustomType<DateOnlyConverter>().Nullable();
        Map(x => x.IsEnabled).Not.Nullable();
        Map(x => x.IsDesired).Not.Nullable();
        Map(x => x.IsPreferred).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.Rating).Nullable();
        Map(x => x.RatingVotes).Nullable();
        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
