using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
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
            Map(x => x.ResumePosition).Not.Nullable();
        }
    }
}