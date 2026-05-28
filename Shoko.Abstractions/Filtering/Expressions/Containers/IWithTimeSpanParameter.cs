using System;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a time-span parameter.
/// </summary>
public interface IWithTimeSpanParameter
{
    /// <summary>
    /// The time-span parameter value.
    /// </summary>
    TimeSpan Parameter { get; set; }
}
