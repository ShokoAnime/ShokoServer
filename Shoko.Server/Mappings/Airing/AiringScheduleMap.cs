using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Airing;

namespace Shoko.Server.Mappings.Airing;

public class AiringScheduleMap : ClassMap<AiringSchedule>
{
    public AiringScheduleMap()
    {
        Table("AiringSchedule");

        Not.LazyLoad();
        Id(x => x.AiringScheduleID);

        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.ProviderName).Not.Nullable();
        Map(x => x.SeriesSource).CustomType<DataSource>().Not.Nullable();
        Map(x => x.SeriesID).Not.Nullable();
        Map(x => x.SeasonID).Not.Nullable();
        // Quoted, since "Key" is a reserved word on some backends.
        Map(x => x.Key).Column("`Key`").Not.Nullable();
        Map(x => x.ChannelID).Nullable();
        Map(x => x.Tracks).CustomType<JsonListConverter<AiringTrackData>>().Not.Nullable();
        Map(x => x.FirstEpisodeNumber).Nullable();
        Map(x => x.LastEpisodeNumber).Nullable();
        Map(x => x.IsFinished).Not.Nullable();
        Map(x => x.TimeZoneID).Nullable();
        Map(x => x.Url).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
