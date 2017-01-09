using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class JMMUserMap : ClassMap<SVR_JMMUser>
    {
        public JMMUserMap()
        {
            Not.LazyLoad();
            Id(x => x.JMMUserID);

            Map(x => x.HideCategories);
            Map(x => x.IsAniDBUser).Not.Nullable();
            Map(x => x.IsTraktUser).Not.Nullable();
            Map(x => x.IsAdmin).Not.Nullable();
            Map(x => x.Password);
            Map(x => x.Username);
            Map(x => x.CanEditServerSettings);
            Map(x => x.PlexUsers);
        }
    }
}