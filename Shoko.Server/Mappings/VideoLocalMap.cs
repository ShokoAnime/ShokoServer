using FluentNHibernate.Mapping;
using Shoko.Models.MediaInfo;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class VideoLocalMap : ClassMap<VideoLocal>
{
    public VideoLocalMap()
    {
        Table("VideoLocal");
        Not.LazyLoad();
        Id(x => x.VideoLocalID);
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.DateTimeCreated).Not.Nullable();
        Map(x => x.DateTimeImported);
#pragma warning disable CS0618
        Map(x => x.FileName).Not.Nullable();
#pragma warning restore CS0618
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.Hash).Not.Nullable();
        Map(x => x.HashSource).Not.Nullable();
        Map(x => x.IsIgnored).Not.Nullable();
        Map(x => x.IsVariation).Not.Nullable();
        Map(x => x.MediaVersion).Not.Nullable();
        Map(x => x.MediaInfo).Nullable().Column("MediaBlob").CustomType<MessagePackConverter<MediaContainer>>();
        Map(x => x.MyListID).Not.Nullable();
        Map(x => x.LastAVDumped);
        Map(x => x.LastAVDumpVersion);
    }
}
