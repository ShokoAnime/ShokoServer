using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class VideoLocal_PlaceMap : ClassMap<SVR_VideoLocal_Place>
    {
        public VideoLocal_PlaceMap()
        {
            Table("VideoLocal_Place");

            Not.LazyLoad();
            Id(x => x.VideoLocal_Place_ID);
            Map(x => x.VideoLocalID).Not.Nullable();
            Map(x => x.FilePath).Not.Nullable();
            Map(x => x.ImportFolderID).Not.Nullable();
            Map(x => x.ImportFolderType).Not.Nullable();
        }
    }
}