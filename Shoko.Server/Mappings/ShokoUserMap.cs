using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class ShokoUserMap : ClassMap<ShokoUser>
{
    public ShokoUserMap()
    {
        Table("ShokoUser");

        Not.LazyLoad();
        Id(x => x.ID);

        Map(x => x.HideCategories);
        Map(x => x.IsAniDBUser).Not.Nullable();
        Map(x => x.IsAdmin).Not.Nullable();
        Map(x => x.Password);
        Map(x => x.Username);
        Map(x => x.PlexUsers);
        Map(x => x.PlexToken);
    }
}
