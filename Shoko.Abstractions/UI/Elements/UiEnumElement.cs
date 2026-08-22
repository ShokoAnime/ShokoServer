using System.Collections.Generic;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A choice between a fixed set of named values.
/// </summary>
public sealed class UiEnumElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Enum;

    /// <summary>
    /// The selectable values, in declaration order.
    /// </summary>
    public IReadOnlyList<UiEnumValue> Values { get; init; } = [];

    /// <summary>
    /// Whether the values are bit flags and can be combined.
    /// </summary>
    public bool IsFlag { get; init; }
}
