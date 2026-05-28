namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a number selector as its left operand.
/// </summary>
public interface IWithNumberSelectorParameter
{
    /// <summary>
    /// The number selector expression on the left side.
    /// </summary>
    FilterExpression<double>? Left { get; set; }
}
