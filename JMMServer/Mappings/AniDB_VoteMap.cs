using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_VoteMap : ClassMap<AniDB_Vote>
    {
        public AniDB_VoteMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_VoteID);

            Map(x => x.EntityID).Not.Nullable();
            Map(x => x.VoteValue).Not.Nullable();
            Map(x => x.VoteType).Not.Nullable();
        }
    }
}