using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class VideoLocal_PlaceMap : ClassMap<VideoLocal_Place>
{
    public VideoLocal_PlaceMap()
    {
        Table("VideoLocal_Place");
        Not.LazyLoad();
        Id(x => x.ID);

        Map(x => x.VideoID).Not.Nullable();
        Map(x => x.ManagedFolderID).Not.Nullable();
        Map(x => x.RelativePath).Not.Nullable();
        Map(x => x.IsKept).Not.Nullable();
        Map(x => x.PrimaryLocationID).Not.Nullable();
        Map(x => x.IsSymbolicLink).Not.Nullable();
        Map(x => x.SymbolicLinkTarget);
    }
}
