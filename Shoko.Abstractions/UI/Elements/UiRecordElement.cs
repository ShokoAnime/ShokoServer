using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A keyed collection of values of a single element kind.
/// </summary>
public sealed class UiRecordElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Record;

    /// <summary>
    /// How the record itself should be laid out.
    /// </summary>
    public DisplayRecordType RecordType { get; init; }

    /// <summary>
    /// The element each key is rendered and validated as.
    /// </summary>
    public UiElement KeyItem { get; init; } = null!;

    /// <summary>
    /// The element each value is rendered and validated as, named to match
    /// <see cref="UiListElement.Item"/> so a renderer can treat the payload of a
    /// list entry and of a record entry the same way.
    /// </summary>
    public UiElement Item { get; init; } = null!;

    /// <summary>
    /// Whether the user may reorder the entries.
    /// </summary>
    public bool Sortable { get; init; }

    /// <summary>
    /// Whether the add-entry affordance is suppressed.
    /// </summary>
    public bool HideAddAction { get; init; }

    /// <summary>
    /// Whether the remove-entry affordance is suppressed.
    /// </summary>
    public bool HideRemoveAction { get; init; }
}
