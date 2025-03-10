using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class ShokoManagedFolderMap : ClassMap<ShokoManagedFolder>
{
    public ShokoManagedFolderMap()
    {
        Table("ImportFolder");
        Not.LazyLoad();
        Id(x => x.ID).Column("ImportFolderID");

        Map(x => x.Path).Column("ImportFolderLocation").Not.Nullable();
        Map(x => x.Name).Column("ImportFolderName").Not.Nullable();
        Map(x => x.IsDropDestination).Not.Nullable();
        Map(x => x.IsDropSource).Not.Nullable();
        Map(x => x.IsWatched).Not.Nullable();
    }
}
