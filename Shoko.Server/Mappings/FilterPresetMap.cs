using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class FilterPresetMap : ClassMap<FilterPreset>
{
    public FilterPresetMap()
    {
        Table("FilterPreset");
        Not.LazyLoad();
        Id(x => x.FilterPresetID);
        Map(x => x.ParentFilterPresetID).Nullable();
        References(a => a.Parent).Column("ParentFilterPresetID").ReadOnly();
        HasMany(x => x.Children).Fetch.Join().KeyColumn("ParentFilterPresetID").ReadOnly();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.FilterType).Not.Nullable().CustomType<FilterPresetType>();
        Map(x => x.Locked).Not.Nullable();
        Map(x => x.Hidden).Not.Nullable();
        Map(x => x.ApplyAtSeriesLevel).Not.Nullable();
        Map(x => x.Expression).Nullable().CustomType<FilterExpressionConverter>();
        Map(x => x.SortingExpression).Nullable().CustomType<FilterExpressionConverter>();
    }
}
