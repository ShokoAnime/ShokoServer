using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class AniDB_MessageMap : ClassMap<AniDB_Message>
{
    public AniDB_MessageMap()
    {
        Table("AniDB_Message");
        Not.LazyLoad();
        Id(x => x.AniDB_MessageID);

        Map(x => x.MessageID).Not.Nullable();
        Map(x => x.FromUserId).Not.Nullable();
        Map(x => x.FromUserName).Not.Nullable();
        Map(x => x.Date).Not.Nullable();
        Map(x => x.Type).Not.Nullable().CustomType<AniDBMessageType>();
        Map(x => x.Title).Not.Nullable();
        Map(x => x.Body).Not.Nullable();
        Map(x => x.Flags).Not.Nullable().CustomType<AniDBMessageFlags>();
    }
}
