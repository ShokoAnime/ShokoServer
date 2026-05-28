using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering;

/// <summary>
/// Represents a saved filter configuration with an expression and optional sorting.
/// </summary>
public interface IFilterPreset
{
    /// <summary>
    ///   Indicates that this filtering should happen at the series level and
    ///   not the group level, which is the default.
    /// </summary>
    bool ApplyAtSeriesLevel { get; }

    /// <summary>
    ///   Indicates that this is a directory filter, and therefore will always
    ///   return empty results.
    /// </summary>
    bool IsDirectory { get; }

    /// <summary>
    ///   The expression to evaluate. Omitting will disable filtering and return
    ///   all results.
    /// </summary>
    IFilterExpression<bool>? Expression { get; }

    /// <summary>
    ///   The sorting expression to evaluate. Leave blank for default sorting.
    /// </summary>
    ISortingExpression? SortingExpression { get; }
}
