using FluentNHibernate;
using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
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
        Map(x => x.LastVideoStreamIndex);
        Map(x => x.LastAudioStreamIndex);
        Map(x => x.LastSubtitleStreamIndex);
        Map(Reveal.Member<VideoLocal_User>("_clientData")).Access.Field().CustomType<JTokenDictionaryConverter>().Column("ClientData");
    }
}
