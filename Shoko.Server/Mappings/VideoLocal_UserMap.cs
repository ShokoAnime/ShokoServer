using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class VideoLocal_UserMap : ClassMap<VideoLocal_User>
{
    public VideoLocal_UserMap()
    {
        Table("VideoLocal_User");
        Not.LazyLoad();
        Id(x => x.VideoLocal_UserID);

        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.VideoLocalID).Not.Nullable();
        Map(x => x.WatchedDate);
        Map(x => x.WatchedCount).Not.Nullable();
        Map(x => x.ResumePosition).Not.Nullable();
        Map(x => x.LastUpdated).Not.Nullable();
    }
}
