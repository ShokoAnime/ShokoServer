using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class FilterPreset : IFilterPreset
{
    #region Database Columns

    public int FilterPresetID { get; set; }

    public int? ParentFilterPresetID { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool ApplyAtSeriesLevel { get; set; }

    public bool Locked { get; set; }

    public FilterPresetType FilterType { get; set; }

    public bool Hidden { get; set; }

    public FilterExpression<bool>? Expression { get; set; }

    public SortingExpression? SortingExpression { get; set; }

    #endregion

    #region Helpers

    public bool IsDirectory => FilterType.HasFlag(FilterPresetType.Directory);

    public virtual FilterPreset? Parent { get; set; }

    public virtual IEnumerable<FilterPreset>? Children { get; set; }

    #endregion

    #region IFilterPreset Implementation

    IFilterExpression<bool>? IFilterPreset.Expression => Expression;

    ISortingExpression? IFilterPreset.SortingExpression => SortingExpression;

    #endregion
}
