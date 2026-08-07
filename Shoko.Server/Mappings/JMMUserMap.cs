using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class JMMUserMap : ClassMap<JMMUser>
{
    public JMMUserMap()
    {
        Table("JMMUser");

        Not.LazyLoad();
        Id(x => x.JMMUserID);

        Map(x => x.HideCategories);
        Map(x => x.IsAniDBUser).Not.Nullable();
        Map(x => x.IsAdmin).Not.Nullable();
        Map(x => x.Password);
        Map(x => x.ExternalAuthID);
        Map(x => x.Username);
        Map(x => x.CanEditServerSettings);
        Map(x => x.PlexUsers);
        Map(x => x.PlexToken);
    }
}
