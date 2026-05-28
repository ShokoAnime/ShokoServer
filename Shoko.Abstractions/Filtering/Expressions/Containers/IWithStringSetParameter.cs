using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a set of strings as its parameter.
/// </summary>
public interface IWithStringSetParameter
{
    /// <summary>
    /// The string set parameter value.
    /// </summary>
    IReadOnlySet<string> Parameter { get; set; }
}
