using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class UserConfigurationMap : ClassMap<UserConfiguration>
{
    public UserConfigurationMap()
    {
        Table("UserConfiguration");

        Not.LazyLoad();
        Id(x => x.UserConfigurationID);

        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.ConfigurationID).Not.Nullable();
        Map(x => x.PluginID).Not.Nullable();
        Map(x => x.Configuration).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}
