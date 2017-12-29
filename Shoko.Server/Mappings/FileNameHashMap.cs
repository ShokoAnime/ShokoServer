using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class FileNameHashMap : ClassMap<FileNameHash>
    {
        public FileNameHashMap()
        {
            Not.LazyLoad();
            Id(x => x.FileNameHashID);

            Map(x => x.Hash);
            Map(x => x.FileName);
            Map(x => x.FileSize).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
        }
    }
}