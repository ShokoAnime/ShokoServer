using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
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