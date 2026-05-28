using System;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a date selector as its left operand.
/// </summary>
public interface IWithDateSelectorParameter
{
    /// <summary>
    /// The date selector expression on the left side.
    /// </summary>
    FilterExpression<DateTime?>? Left { get; set; }
}
