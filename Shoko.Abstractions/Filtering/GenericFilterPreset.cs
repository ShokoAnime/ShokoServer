using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering;

/// <summary>
///   A generic filter preset to use with the <see cref="IFilteringEngine"/>.
/// </summary>
public class GenericFilterPreset : IFilterPreset
{
    /// <inheritdoc />
    public bool ApplyAtSeriesLevel { get; set; } = true;

    /// <inheritdoc />
    public bool IsDirectory => false;

    /// <inheritdoc />
    public IFilterExpression<bool>? Expression { get; init; }

    /// <inheritdoc />
    public ISortingExpression? SortingExpression { get; init; }
}
