using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a string set selector as its left operand.
/// </summary>
public interface IWithStringSetSelectorParameter
{
    /// <summary>
    /// The string set selector expression on the left side.
    /// </summary>
    FilterExpression<IReadOnlySet<string>>? Left { get; set; }
}
