using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class ScanMap : ClassMap<Scan>
    {
        public ScanMap()
        {
            Not.LazyLoad();
            Id(x => x.ScanID);
            Map(x => x.CreationTIme).Not.Nullable();
            Map(x => x.ImportFolders).Not.Nullable();
            Map(x => x.Status).Not.Nullable();
        }
    }
}
