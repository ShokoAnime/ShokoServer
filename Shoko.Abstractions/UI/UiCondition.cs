using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.UI.Enums;

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
    /// How the value at <see cref="Path"/> is compared.
    /// </summary>
    public UiConditionOperator Operator { get; init; }

    /// <summary>
    /// The value to compare against, for an operator that takes one.
    /// </summary>
    public JToken? Value { get; init; }

    /// <summary>
    /// The values to compare against, for
    /// <see cref="UiConditionOperator.In"/> and
    /// <see cref="UiConditionOperator.NotIn"/>.
    /// </summary>
    public IReadOnlyList<JToken?>? Values { get; init; }
}
