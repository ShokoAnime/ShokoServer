using System;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a date parameter.
/// </summary>
public interface IWithDateParameter
{
    /// <summary>
    /// The date parameter value, or <c>null</c> when it is not set.
    /// </summary>
    DateTime? Parameter { get; set; }
}
