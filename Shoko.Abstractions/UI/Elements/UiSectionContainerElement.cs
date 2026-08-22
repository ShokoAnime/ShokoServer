using System.Collections.Generic;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A container holding an ordered set of elements grouped into sections.
/// </summary>
public sealed class UiSectionContainerElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.SectionContainer;

    /// <summary>
    /// How the sections should be laid out.
    /// </summary>
    public DisplaySectionType SectionType { get; init; }

    /// <summary>
    /// The name of the section that holds items without an explicit one.
    /// </summary>
    public string DefaultSectionName { get; init; } = "Default";

    /// <summary>
    /// Whether sections assembled from items without an explicit section go
    /// after the other sections instead of before them.
    /// </summary>
    public bool AppendFloatingSectionsAtEnd { get; init; }

    /// <summary>
    /// Whether the container renders the built-in save action.
    /// </summary>
    public bool ShowSaveAction { get; init; }

    /// <summary>
    /// The key in <see cref="Items"/> of the element that identifies an
    /// instance of this container when the container is a list item, or
    /// <c>null</c> when there is none.
    /// </summary>
    public string? PrimaryKey { get; init; }

    /// <summary>
    /// The elements the user edits, keyed by the property name they are stored
    /// under in the configuration document — the same key
    /// <see cref="UiStructureEntry.Name"/> carries for a
    /// <see cref="UiStructureMemberKind.Item"/> entry, so a client can index
    /// straight into this rather than scanning for a match.
    /// </summary>
    /// <remarks>
    /// Enumerates in <see cref="Structure"/> order, so a client that renders the
    /// values in order gets the authored layout without consulting
    /// <see cref="Structure"/> at all.
    /// </remarks>
    public IReadOnlyDictionary<string, UiElement> Items { get; init; } = new Dictionary<string, UiElement>();

    /// <summary>
    /// The actions attached to this container, keyed by
    /// <see cref="UiAction.ID"/> — the same key
    /// <see cref="UiStructureEntry.Name"/> carries for a
    /// <see cref="UiStructureMemberKind.Action"/> entry.
    /// </summary>
    /// <remarks>
    /// Enumerates in <see cref="Structure"/> order, the same as
    /// <see cref="Items"/>.
    /// </remarks>
    public IReadOnlyDictionary<string, UiAction> Actions { get; init; } = new Dictionary<string, UiAction>();

    /// <summary>
    /// <see cref="Items"/> and <see cref="Actions"/> interleaved in the
    /// order their members were authored, so a client can place an action button
    /// between two fields. Each entry names which of the two it points into.
    /// </summary>
    public IReadOnlyList<UiStructureEntry> Structure { get; init; } = [];
}
