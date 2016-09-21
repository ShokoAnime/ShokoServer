using FluentNHibernate.Mapping;
using JMMServer.Entities;

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
