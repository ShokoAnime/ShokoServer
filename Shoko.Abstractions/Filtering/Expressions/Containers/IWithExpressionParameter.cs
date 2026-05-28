namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts another boolean expression as its left operand.
/// </summary>
public interface IWithExpressionParameter
{
    /// <summary>
    /// The boolean expression on the left side.
    /// </summary>
    FilterExpression<bool>? Left { get; set; }
}
