using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI;

/// <summary>
/// One entry in a container's — or one of its sections' — render order,
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
    /// <see cref="Elements.UiSectionContainerElement.Actions"/> — where the key
    /// is the action's <see cref="UiAction.Name"/> — or
    /// <see cref="Elements.UiSectionContainerElement.FloatingSections"/>, where
    /// it is the section's <see cref="UiFloatingSection.Title"/>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Which of the container's maps <see cref="Name"/> points into.
    /// </summary>
    public UiStructureMemberKind Kind { get; init; }
}

/// <summary>
/// What a <see cref="UiStructureEntry"/> refers to.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum UiStructureMemberKind
{
    /// <summary>
    /// An element the user edits.
    /// </summary>
    [EnumMember(Value = "item")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("item")]
    Item = 0,

    /// <summary>
    /// An action the user invokes.
    /// </summary>
    [EnumMember(Value = "action")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("action")]
    Action = 1,

    /// <summary>
    /// A group of members assembled out of the container, to render together
    /// under a heading of its own.
    /// </summary>
    [EnumMember(Value = "floating-section")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("floating-section")]
    FloatingSection = 2,
}
