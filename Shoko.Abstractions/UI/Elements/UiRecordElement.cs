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
    /// The path within an entry's value to read its title from, relative to the
    /// value, or <c>null</c> to label the entry with the key it is stored under.
    /// </summary>
    /// <remarks>
    /// Set when the value's class holds a
    /// <see cref="Components.TitleComponent"/> saying what it calls itself, or
    /// carries the member marked <c>[Key]</c>.
    /// </remarks>
    public string? ItemTitlePath { get; init; }

    /// <summary>
    /// The path within an entry's value to read the line under its title from,
    /// or <c>null</c> when there is none.
    /// </summary>
    public string? ItemCategoryPath { get; init; }

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
