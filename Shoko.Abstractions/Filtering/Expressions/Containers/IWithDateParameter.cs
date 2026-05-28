using System;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a date parameter.
/// </summary>
public interface IWithDateParameter
{
    /// <summary>
    /// The date parameter value.
    /// </summary>
    DateTime Parameter { get; set; }
}
