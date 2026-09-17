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
    /// <remarks>
    /// Inside a list item or a record value, the path resolves against that one
    /// item or value. It can only descend, never reach a value outside the
    /// enclosing object: the definition describes a type, not an instance, so it
    /// cannot know where that object sits in the document.
    /// </remarks>
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
