using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class RenamerConfigMap : ClassMap<RenamerConfig>
{
    public RenamerConfigMap()
    {
        // We could rename this, but it already exists in test databases....
        Table("RenamerInstance");
        Not.LazyLoad();
        Id(x => x.ID);

        Map(x => x.Name);
        Map(x => x.Type).CustomType<TypeStringConverter>().Not.Nullable();
        Map(x => x.Settings).Nullable().CustomType<TypelessMessagePackConverter>();
    }
}
