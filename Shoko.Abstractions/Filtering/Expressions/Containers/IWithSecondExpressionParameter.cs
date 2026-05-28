namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts another boolean expression as its right operand.
/// </summary>
public interface IWithSecondExpressionParameter
{
    /// <summary>
    /// The boolean expression on the right side.
    /// </summary>
    FilterExpression<bool>? Right { get; set; }
}
