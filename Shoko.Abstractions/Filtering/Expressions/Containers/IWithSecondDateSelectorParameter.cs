using System;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a date selector as its right operand.
/// </summary>
public interface IWithSecondDateSelectorParameter
{
    /// <summary>
    /// The date selector expression on the right side.
    /// </summary>
    FilterExpression<DateTime?>? Right { get; set; }
}
