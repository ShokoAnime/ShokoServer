using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Server.Filters;

namespace Shoko.Server.Models;

public class FilterPreset
{
    public int FilterPresetID { get; set; }
    public virtual FilterPreset Parent { get; set; }
    public virtual IEnumerable<FilterPreset> Children { get; set; }
    public int? ParentFilterPresetID { get; set; }
    public string Name { get; set; }
    public bool ApplyAtSeriesLevel { get; set; }
    public bool Locked { get; set; }
    public GroupFilterType FilterType { get; set; }
    public bool Hidden { get; set; }

    public FilterExpression<bool> Expression { get; set; }
    public SortingExpression SortingExpression { get; set; }
}
