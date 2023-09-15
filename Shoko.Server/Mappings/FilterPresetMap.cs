using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class FilterPresetMap : ClassMap<FilterPreset>
{
    public FilterPresetMap()
    {
        Table("FilterPreset");
        Not.LazyLoad();
        Id(x => x.FilterPresetID);
        Map(x => x.ParentFilterPresetID).Nullable();
        //References(x => x.Parent).Nullable().PropertyRef(x => x.ParentFilterID);
        Map(x => x.Name).Not.Nullable();
        Map(x => x.FilterType).Not.Nullable().CustomType<GroupFilterType>();
        Map(x => x.Locked).Not.Nullable();
        Map(x => x.Hidden).Not.Nullable();
        Map(x => x.ApplyAtSeriesLevel).Not.Nullable();
        Map(x => x.Expression).Nullable().CustomType<FilterExpressionConverter>();
        Map(x => x.SortingExpression).Nullable().CustomType<FilterExpressionConverter>();
    }
}
