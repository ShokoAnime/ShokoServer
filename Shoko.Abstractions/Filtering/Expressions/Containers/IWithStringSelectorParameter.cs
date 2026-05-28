namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a string selector as its left operand.
/// </summary>
public interface IWithStringSelectorParameter
{
    /// <summary>
    /// The string selector expression on the left side.
    /// </summary>
    FilterExpression<string>? Left { get; set; }
}
