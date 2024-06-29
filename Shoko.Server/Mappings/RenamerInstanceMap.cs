using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class RenamerInstanceMap : ClassMap<RenamerInstance>
{
    public RenamerInstanceMap()
    {
        Not.LazyLoad();
        Id(x => x.ID);

        Map(x => x.Name);
        Map(x => x.Type).CustomType<TypeStringConverter>().Not.Nullable();
        Map(x => x.Settings).Nullable().CustomType<TypelessMessagePackConverter>();
    }
}
