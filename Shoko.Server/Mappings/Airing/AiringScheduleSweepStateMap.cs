using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;

namespace Shoko.Server.Mappings.Airing;

public class AiringScheduleSweepStateMap : ClassMap<AiringScheduleSweepState>
{
    public AiringScheduleSweepStateMap()
    {
        Table("AiringScheduleSweepState");

        Not.LazyLoad();
        Id(x => x.AiringScheduleSweepStateID);

        Map(x => x.ProviderID).Not.Nullable().Unique();
        // Quoted, since "Cursor" is a reserved word on some backends.
        Map(x => x.Cursor).Column("`Cursor`").Nullable();
        Map(x => x.LastRunAt).Not.Nullable();
        Map(x => x.LastOutcome).CustomType<AiringScheduleSweepOutcome>().Not.Nullable();
        Map(x => x.NoProgressCount).Not.Nullable();
    }
}
