using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_ReleaseGroupMap : ClassMap<SVR_AniDB_ReleaseGroup>
    {
        public AniDB_ReleaseGroupMap()
        {
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