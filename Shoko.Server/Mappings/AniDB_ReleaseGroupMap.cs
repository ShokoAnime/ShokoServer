using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_ReleaseGroupMap : ClassMap<AniDB_ReleaseGroup>
    {
        public AniDB_ReleaseGroupMap()
        {
            Table("AniDB_ReleaseGroup");
            Not.LazyLoad();
            Id(x => x.AniDB_ReleaseGroupID);

            Map(x => x.GroupID).Not.Nullable();
            Map(x => x.Rating).Not.Nullable();
            Map(x => x.Votes).Not.Nullable();
            Map(x => x.AnimeCount).Not.Nullable();
            Map(x => x.FileCount).Not.Nullable();

            Map(x => x.GroupName);
            Map(x => x.GroupNameShort);
            Map(x => x.IRCChannel);
            Map(x => x.IRCServer);
            Map(x => x.URL);
            Map(x => x.Picname);
        }
    }
}