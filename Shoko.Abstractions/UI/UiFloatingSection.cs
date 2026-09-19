using System.Collections.Generic;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A titled group assembled out of a container's members, rather than one that
/// a member is in its own right.
/// </summary>
/// <remarks>
/// <para>
/// These are the sections authored with a <c>SectionNameAttribute</c>, plus the
/// default section a container gathers its unnamed members into. A member that
/// is a container on its own account — a nested configuration class, or a list
/// of them — is not one of these: it stays an ordinary item, and renders its own
/// heading from its own label.
/// </para>
/// <para>
/// A section is assembled when the definition is built, so a client renders what
/// it is given instead of grouping members itself. How the group is drawn — a
/// tab, a field set — is up to
/// <see cref="Elements.UiSectionContainerElement.SectionType"/>.
/// </para>
/// <para>
/// Sections are static: a member hidden by its visibility is still listed, so a
/// client should skip a section once every member in it is hidden.
/// </para>
/// </remarks>
public class UiFloatingSection
{
    /// <summary>
    /// The title of the section, which is also the key it is filed under in
    /// <see cref="Elements.UiSectionContainerElement.FloatingSections"/>.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// An optional longer description of the section.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether any container among this section's members, or below one, handles
    /// live edits.
    /// </summary>
    /// <remarks>
    /// The section's own members are handled by the container's class, so
    /// <see cref="Elements.UiSectionContainerElement.HasLiveEdit"/> covers those;
    /// this is what tells one section of a container apart from another.
    /// </remarks>
    public bool HasNestedLiveEdit { get; init; }

    /// <summary>
    /// The actions pinned to the top of the section, keyed by
    /// <see cref="UiAction.Name"/>, in the order they were authored.
    /// </summary>
    public IReadOnlyList<string> StartActions { get; init; } = [];

    /// <summary>
    /// The actions pinned to the bottom of the section, keyed by
    /// <see cref="UiAction.Name"/>, in the order they were authored.
    /// </summary>
    /// <remarks>
    /// The built-in save action, when
    /// <see cref="Elements.UiSectionContainerElement.ShowSaveAction"/> is set,
    /// renders after these.
    /// </remarks>
    public IReadOnlyList<string> EndActions { get; init; } = [];

    /// <summary>
    /// The section's members, in the order they were authored. Only items and
    /// inline actions: a section holds no sections of its own.
    /// </summary>
    /// <remarks>
    /// Actions run together into one button row while they are adjacent, rather
    /// than each getting a row of its own.
    /// </remarks>
    public IReadOnlyList<UiStructureEntry> Structure { get; init; } = [];
}
