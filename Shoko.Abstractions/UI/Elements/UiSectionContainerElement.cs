using System.Collections.Generic;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A container holding an ordered set of elements, some of them grouped into
/// sections.
/// </summary>
public sealed class UiSectionContainerElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.SectionContainer;

    /// <summary>
    /// How the container's members should be laid out.
    /// </summary>
    public DisplaySectionType SectionType { get; init; }

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
    /// An item that is a container itself renders its own heading from its own
    /// label; it is not listed in <see cref="FloatingSections"/>.
    /// </remarks>
    public IReadOnlyDictionary<string, UiElement> Items { get; init; } = new Dictionary<string, UiElement>();

    /// <summary>
    /// The actions attached to this container, keyed by
    /// <see cref="UiAction.ID"/> — the same key
    /// <see cref="UiStructureEntry.Name"/> carries for a
    /// <see cref="UiStructureMemberKind.Action"/> entry.
    /// </summary>
    public IReadOnlyDictionary<string, UiAction> Actions { get; init; } = new Dictionary<string, UiAction>();

    /// <summary>
    /// The groups assembled out of this container's members, keyed by their
    /// title — the same key <see cref="UiStructureEntry.Name"/> carries for a
    /// <see cref="UiStructureMemberKind.FloatingSection"/> entry.
    /// </summary>
    /// <remarks>
    /// These are the sections authored with a section name, plus the default
    /// section the container gathers its unnamed members into when it needs one.
    /// </remarks>
    public IReadOnlyDictionary<string, UiFloatingSection> FloatingSections { get; init; } = new Dictionary<string, UiFloatingSection>();

    /// <summary>
    /// The actions pinned to the top of the container, outside every section,
    /// keyed by <see cref="UiAction.ID"/>.
    /// </summary>
    public IReadOnlyList<string> StartActions { get; init; } = [];

    /// <summary>
    /// The actions pinned to the bottom of the container, outside every section,
    /// keyed by <see cref="UiAction.ID"/>.
    /// </summary>
    /// <remarks>
    /// The built-in save action, when <see cref="ShowSaveAction"/> is set,
    /// renders after these.
    /// </remarks>
    public IReadOnlyList<string> EndActions { get; init; } = [];

    /// <summary>
    /// The container's members in the order they were authored, each entry
    /// naming the map to look it up in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A member that was not given a section name keeps its place here, so a
    /// container laid out as a field set can go item, item, nested container,
    /// item. Members sharing a section name are gathered into one
    /// <see cref="UiFloatingSection"/>, entered here at the first of them.
    /// </para>
    /// <para>
    /// A container laid out as tabs, or one whose class named its default
    /// section, gathers its remaining loose members into one more section
    /// instead of leaving them here — a tab has to have a label. The authored
    /// <c>AppendFloatingSectionsAtEnd</c> puts every gathered section after the
    /// rest of the entries.
    /// </para>
    /// </remarks>
    public IReadOnlyList<UiStructureEntry> Structure { get; init; } = [];
}
