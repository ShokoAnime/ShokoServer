using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// An ordered collection of items of a single element kind.
/// </summary>
public sealed class UiListElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.List;

    /// <summary>
    /// How the list itself should be laid out.
    /// </summary>
    public DisplayListType ListType { get; init; }

    /// <summary>
    /// The element used to render and validate each item.
    /// </summary>
    public UiElement Item { get; init; } = null!;

    /// <summary>
    /// Whether the user may reorder the items.
    /// </summary>
    public bool Sortable { get; init; }

    /// <summary>
    /// Whether duplicate items are rejected.
    /// </summary>
    public bool UniqueItems { get; init; }

    /// <summary>
    /// Whether the add-item affordance is suppressed.
    /// </summary>
    public bool HideAddAction { get; init; }

    /// <summary>
    /// Whether the remove-item affordance is suppressed.
    /// </summary>
    public bool HideRemoveAction { get; init; }

    /// <summary>
    /// The fewest items the list may hold, or <c>null</c> when unbounded.
    /// </summary>
    public int? MinItems { get; init; }

    /// <summary>
    /// The most items the list may hold, or <c>null</c> when unbounded.
    /// </summary>
    public int? MaxItems { get; init; }

    /// <summary>
    /// Dotted path, relative to an item, to the value to use as the item's
    /// primary label, or <c>null</c> to stringify the item itself.
    /// </summary>
    public string? ItemTitlePath { get; init; }

    /// <summary>
    /// Dotted path, relative to an item, to the value to use as the item's
    /// secondary/category label, or <c>null</c> when there is none.
    /// </summary>
    public string? ItemCategoryPath { get; init; }
}
