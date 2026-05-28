using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering.Models;

/// <summary>
///   Input data for updating an existing filter preset.
/// </summary>
public class FilterPresetUpdateData
{
    /// <summary>
    ///   The name of the filter.
    /// </summary>
    public string? Name { get; set; }

    private int? _parentFilterID;

    /// <summary>
    ///   Indicates if the parent filter ID has been set.
    /// </summary>
    public bool ParentFilterIDSet { get; private set; }

    /// <summary>
    ///   The ID of the parent filter, if this is a sub-filter.
    /// </summary>
    public int? ParentFilterID
    {
        get => _parentFilterID;
        set
        {
            ParentFilterIDSet = true;
            _parentFilterID = value;
        }
    }

    /// <summary>
    ///   Indicates the filter should be hidden from normal UIs.
    /// </summary>
    public bool? IsHidden { get; set; }

    /// <summary>
    ///   Indicates the filter should be applied at the series level rather than the group level.
    /// </summary>
    public bool? ApplyAtSeriesLevel { get; set; }

    private FilterExpression<bool>? _expression;

    /// <summary>
    ///   Indicates if the filter expression has been set.
    /// </summary>
    public bool ExpressionSet { get; private set; }

    /// <summary>
    ///   The filter expression tree as a JSON-compatible structure.
    /// </summary>
    public FilterExpression<bool>? Expression
    {
        get => _expression;
        set
        {
            ExpressionSet = true;
            _expression = value;
        }
    }

    private SortingExpression? _sorting;

    /// <summary>
    ///   Indicates if the sorting criteria has been set.
    /// </summary>
    public bool SortingSet { get; private set; }

    /// <summary>
    ///   The sorting criteria.
    /// </summary>
    public SortingExpression? Sorting
    {
        get => _sorting;
        set
        {
            SortingSet = true;
            _sorting = value;
        }
    }
}
