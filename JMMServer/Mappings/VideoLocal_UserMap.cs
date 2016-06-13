using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class VideoLocal_UserMap : ClassMap<VideoLocal_User>
    {
        public VideoLocal_UserMap()
        {
            Not.LazyLoad();
            Id(x => x.VideoLocal_UserID);

            Map(x => x.JMMUserID).Not.Nullable();
            Map(x => x.VideoLocalID).Not.Nullable();
            Map(x => x.WatchedDate);
        }
    }
}