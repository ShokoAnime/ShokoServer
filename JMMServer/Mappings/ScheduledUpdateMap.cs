using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
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