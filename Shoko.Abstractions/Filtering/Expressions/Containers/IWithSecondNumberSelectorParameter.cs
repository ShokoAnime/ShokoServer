namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a number selector as its right operand.
/// </summary>
public interface IWithSecondNumberSelectorParameter
{
    /// <summary>
    /// The number selector expression on the right side.
    /// </summary>
    FilterExpression<double>? Right { get; set; }
}
