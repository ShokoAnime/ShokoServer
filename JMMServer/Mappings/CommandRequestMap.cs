using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace NHibernateTest.Mappings
{
    public class CommandRequestMap : ClassMap<CommandRequest>
    {
        public CommandRequestMap()
        {
            Not.LazyLoad();
            Id(x => x.CommandRequestID);
            Map(x => x.CommandDetails).Not.Nullable();
            Map(x => x.CommandID).Not.Nullable();
            Map(x => x.CommandType).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.Priority).Not.Nullable();
        }
    }
}