using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Airing;

namespace Shoko.Server.Mappings.Airing;

public class EpisodeAiringMap : ClassMap<EpisodeAiring>
{
    public EpisodeAiringMap()
    {
        Table("EpisodeAiring");

        Not.LazyLoad();
        Id(x => x.EpisodeAiringID);

        Map(x => x.AiringScheduleID).Not.Nullable();
        // Quoted, since "Key" is a reserved word on some backends.
        Map(x => x.Key).Column("`Key`").Not.Nullable();
        Map(x => x.EpisodeSource).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EpisodeID).Not.Nullable();
        Map(x => x.Url).Nullable();
        Map(x => x.AiredAt).Nullable();
        Map(x => x.OriginalAiredAt).Nullable();
        Map(x => x.IsDelayed).Not.Nullable();
        Map(x => x.Kind).CustomType<EpisodeAiringKind>().Not.Nullable();
        Map(x => x.LinkedToID).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
