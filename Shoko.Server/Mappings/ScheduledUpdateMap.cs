using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class ScheduledUpdateMap : ClassMap<ScheduledUpdate>
    {
        public ScheduledUpdateMap()
        {
            Not.LazyLoad();
            Id(x => x.ScheduledUpdateID);

            Map(x => x.LastUpdate).Not.Nullable();
            Map(x => x.UpdateDetails);
            Map(x => x.UpdateType).Not.Nullable();
        }
    }
}