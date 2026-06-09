using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering;

/// <summary>
///   A filter preset stored in the database, extending <see cref="IFilter"/>
///   with metadata, persistence properties, and the <see cref="IsDirectory"/> flag.
/// </summary>
public interface IFilterPreset : IFilter, IMetadata<int>
{
    /// <summary>
    ///   The id of the parent filter preset if this is a sub-filter.
    /// </summary>
    int? ParentFilterID { get; }

    /// <summary>
    ///   The name of this filter preset.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Indicates that this filter cannot be edited.
    /// </summary>
    bool Locked { get; }

    /// <summary>
    ///   Indicates that this filter should be hidden from normal UIs.
    /// </summary>
    bool Hidden { get; }

    /// <summary>
    ///   Indicates that this is a directory filter, and therefore will always
    ///   return empty results.
    /// </summary>
    bool IsDirectory { get; }

    /// <summary>
    ///   The expression to evaluate. Plugins should use the typed <see cref="IFilter.Expression"/> property.
    /// </summary>
    new FilterExpression<bool>? Expression { get; }

    /// <summary>
    ///   The sorting expression to evaluate.
    /// </summary>
    new SortingExpression? SortingExpression { get; }
}
