using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class VersionsMap : ClassMap<Versions>
    {
        public VersionsMap()
        {
            Not.LazyLoad();
            Id(x => x.VersionsID);
            Map(x => x.VersionType).Not.Nullable();
            Map(x => x.VersionValue).Not.Nullable();
        }
    }
}