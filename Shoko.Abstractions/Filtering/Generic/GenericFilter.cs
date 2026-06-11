using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering.Generic;

/// <summary>
///   A generic filter to use with the <see cref="IFilteringEngine"/>.
/// </summary>
public sealed class GenericFilter : IFilter
{
    /// <inheritdoc />
    public bool ApplyAtSeriesLevel { get; set; } = true;

    /// <inheritdoc />
    public IFilterExpression<bool>? Expression { get; init; }

    /// <inheritdoc />
    public ISortingExpression? SortingExpression { get; init; }
}
