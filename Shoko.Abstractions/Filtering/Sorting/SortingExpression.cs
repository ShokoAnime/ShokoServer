using Shoko.Abstractions.Filtering.Expressions;

namespace Shoko.Abstractions.Filtering.Sorting;

/// <summary>
/// Base class for sorting expressions that determine the order of filter results.
/// </summary>
public abstract class SortingExpression : FilterExpression<object>, ISortingExpression
{
    /// <inheritdoc/>
    public bool Descending { get; set; }

    /// <inheritdoc/>
    public SortingExpression? Next { get; set; }

    ISortingExpression? ISortingExpression.Next => Next;
}
