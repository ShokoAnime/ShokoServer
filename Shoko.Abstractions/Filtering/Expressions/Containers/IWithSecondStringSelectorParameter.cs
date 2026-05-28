namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a string selector as its right operand.
/// </summary>
public interface IWithSecondStringSelectorParameter
{
    /// <summary>
    /// The string selector expression on the right side.
    /// </summary>
    FilterExpression<string>? Right { get; set; }
}
