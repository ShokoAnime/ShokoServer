using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class StoredRelocationPresetMap : ClassMap<StoredRelocationPreset>
{
    public StoredRelocationPresetMap()
    {
        Table("StoredRelocationPreset");
        Not.LazyLoad();
        Id(x => x.StoredRelocationPresetID);

        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.IsDefault).Not.Nullable();
        Map(x => x.Configuration).Nullable();
    }
}
