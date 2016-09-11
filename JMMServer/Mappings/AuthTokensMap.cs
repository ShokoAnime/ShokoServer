using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class AuthTokensMap : ClassMap<AuthTokens>
    {
        public AuthTokensMap()
        {
            Not.LazyLoad();

            Id(x => x.AuthID);
            Map(x => x.UserID).Not.Nullable();
            Map(x => x.DeviceName).Not.Nullable();
            Map(x => x.Token).Not.Nullable();
        }
    }
}
