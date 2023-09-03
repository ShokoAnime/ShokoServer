using Shoko.Models.Enums;

namespace Shoko.Server.Models.Filters;

public class Filter
{
    public int FilterID { get; set; }
    //public virtual Filter Parent { get; set; }
    public int? ParentFilterID { get; set; }
    public string Name { get; set; }
    public bool ApplyAtSeriesLevel { get; set; }
    public bool Locked { get; set; }
    public GroupFilterType FilterType { get; set; }
    public bool Hidden { get; set; }

    public FilterExpression<bool> Expression { get; set; }
    public SortingExpression SortingExpression { get; set; }
}
