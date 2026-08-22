using Newtonsoft.Json.Linq;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A condition evaluated against another value in the same configuration.
/// </summary>
public class UiCondition
{
    /// <summary>
    /// Dotted path to the value to compare, relative to the nearest enclosing
    /// object.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The value the path has to equal for the condition to hold.
    /// </summary>
    public JToken? Value { get; init; }

    /// <summary>
    /// Whether the outcome of the comparison should be inverted.
    /// </summary>
    public bool InverseCondition { get; init; }
}
