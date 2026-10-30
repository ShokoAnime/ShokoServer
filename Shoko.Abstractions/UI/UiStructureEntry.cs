namespace Shoko.Abstractions.UI;

/// <summary>
/// One entry in the render order of a container or one of its sections,
/// pointing into whichever of the container's maps <see cref="Kind"/> names.
/// </summary>
/// <remarks>
/// A container files its members in three maps by what they are; the structure
/// puts them back in the order they were authored, so a client that wants to
/// place an action button between two fields, or a group of fields between two
/// of them, knows where it goes.
/// </remarks>
public class UiStructureEntry
{
    /// <summary>
    /// The key to look the member up by, in the map named by <see cref="Kind"/>:
    /// <see cref="Elements.UiSectionContainerElement.Items"/>,
    /// <see cref="Elements.UiSectionContainerElement.Actions"/>, where the key
    /// is the action's <see cref="UiAction.Name"/>, or
    /// <see cref="Elements.UiSectionContainerElement.FloatingSections"/>, where
    /// it is the section's <see cref="UiFloatingSection.Title"/>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Which of the container's maps <see cref="Name"/> points into.
    /// </summary>
    public UiStructureMemberKind Kind { get; init; }
}
