using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering.Models;

/// <summary>
///   Input data for creating a new filter preset.
/// </summary>
public class FilterPresetData
{
    /// <summary>
    ///   The name of the filter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   The ID of the parent filter, if this is a sub-filter.
    /// </summary>
    public int? ParentFilterID { get; set; }

    /// <summary>
    ///   Indicates this is a directory filter (cannot have conditions or sorting).
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    ///   Indicates the filter should be hidden from normal UIs.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    ///   Indicates the filter should be applied at the series level rather than the group level.
    /// </summary>
    public bool ApplyAtSeriesLevel { get; set; }

    /// <summary>
    ///   The filter expression tree as a JSON-compatible structure.
    /// </summary>
    public FilterExpression<bool>? Expression { get; set; }

    /// <summary>
    ///   The sorting criteria.
    /// </summary>
    public SortingExpression? Sorting { get; set; }
}
