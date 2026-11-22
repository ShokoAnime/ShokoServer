using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class ShokoManagedFolderMap : ClassMap<ShokoManagedFolder>
{
    public ShokoManagedFolderMap()
    {
        Table("ShokoManagedFolder");
        Not.LazyLoad();
        Id(x => x.ID);

        Map(x => x.Path).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.IsDropDestination).Not.Nullable();
        Map(x => x.IsDropSource).Not.Nullable();
        Map(x => x.IsWatched).Not.Nullable();
        Map(x => x.IsNetworkShare);
    }
}
