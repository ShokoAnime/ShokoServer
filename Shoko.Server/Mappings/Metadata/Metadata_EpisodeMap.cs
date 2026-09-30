using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_EpisodeMap : ClassMap<Metadata_Episode>
{
    public Metadata_EpisodeMap()
    {
        Table("Metadata_Episode");

        Not.LazyLoad();
        Id(x => x.Metadata_EpisodeID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.SeriesID).Not.Nullable();
        Map(x => x.SeasonID).Nullable();
        Map(x => x.SeasonNumber).Nullable();
        Map(x => x.EpisodeNumber).Not.Nullable();
        Map(x => x.Type).CustomType<EpisodeType>().Not.Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.RatingVotes).Not.Nullable();
        Map(x => x.RuntimeSeconds).Column("Runtime").Not.Nullable();
        Map(x => x.AirDate).CustomType<DateOnlyConverter>().Nullable();
        Map(x => x.AirDateWithTime).Nullable();
        Map(x => x.Resources).CustomType<JsonListConverter<Resource>>().Nullable();
        Map(x => x.CrossSourceIDs).CustomType<JsonListConverter<MetadataGuid>>().Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.IsHidden).Not.Nullable();
    }
}
