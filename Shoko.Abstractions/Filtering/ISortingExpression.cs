
namespace Shoko.Abstractions.Filtering;

/// <summary>
/// A sorting expression.
/// </summary>
public interface ISortingExpression : IFilterExpression<object?>
{
    /// <summary>
    /// Determines if the order should be inverted.
    /// </summary>
    bool Descending { get; }

    /// <summary>
    /// The next sorting expression in the chain.
    /// </summary>
    ISortingExpression? Next { get; }
}
