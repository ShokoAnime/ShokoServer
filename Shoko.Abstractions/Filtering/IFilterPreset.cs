using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Filtering;

/// <summary>
/// 
/// </summary>
public interface IFilterPreset
{
    /// <summary>
    ///   Indicates that this filtering should happen at the series level and
    ///   not the group level, which is the default.
    /// </summary>
    bool ApplyAtSeriesLevel { get; }

    /// <summary>
    /// ///  Indicates that this is a directory filter.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Expression), nameof(SortingExpression))]
    bool IsDirectory { get; }

    /// <summary>
    /// The expression to evaluate. Omitting will disable filtering and return
    /// all results.
    /// </summary>
    IFilterExpression<bool>? Expression { get; }

    /// <summary>
    /// The sorting expression to evaluate. Leave blank for default sorting.
    /// </summary>
    ISortingExpression? SortingExpression { get; }
}
