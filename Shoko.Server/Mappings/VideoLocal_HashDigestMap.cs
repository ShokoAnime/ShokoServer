using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class VideoLocal_HashDigestMap : ClassMap<VideoLocal_HashDigest>
{
    public VideoLocal_HashDigestMap()
    {
        Table("VideoLocal_HashDigest");
        Not.LazyLoad();
        Id(x => x.VideoLocal_HashDigestID);

        Map(x => x.VideoLocalID).Not.Nullable();
        Map(x => x.Type).Not.Nullable();
        Map(x => x.Value).Not.Nullable();
        Map(x => x.Metadata);
    }
}
