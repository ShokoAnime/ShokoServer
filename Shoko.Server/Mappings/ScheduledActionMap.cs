using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ScheduledActionMap : ClassMap<ScheduledAction>
{
    public ScheduledActionMap()
    {
        Table("ScheduledAction");

        Not.LazyLoad();
        Id(x => x.ScheduledActionID);

        Map(x => x.ActionID).Not.Nullable().Unique();
        Map(x => x.Triggers).Nullable();
        Map(x => x.LastRunAt).Nullable();
        Map(x => x.LastScheduledRunAt).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
    }
}
