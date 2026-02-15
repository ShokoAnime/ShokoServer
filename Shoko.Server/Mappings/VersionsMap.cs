using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class VersionsMap : ClassMap<Versions>
{
    public VersionsMap()
    {
        Table("Versions");
        Not.LazyLoad();
        Id(x => x.VersionsID);
        Map(x => x.VersionType).Not.Nullable();
        Map(x => x.VersionValue).Not.Nullable();
        Map(x => x.VersionRevision).Nullable();
        Map(x => x.VersionCommand).Nullable();
        Map(x => x.VersionProgram).Nullable();
    }
}
