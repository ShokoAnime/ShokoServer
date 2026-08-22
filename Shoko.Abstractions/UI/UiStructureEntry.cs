using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI;

/// <summary>
/// The authored order of a container's members, actions included.
/// </summary>
/// <remarks>
/// <see cref="Elements.UiSectionContainerElement.Items"/> and
/// <see cref="Elements.UiSectionContainerElement.Actions"/> each enumerate in
/// render order already, but they are two separate maps; this one interleaves
/// them, so a client that wants to place an action button between two fields
/// knows where it goes.
/// </remarks>
public class UiStructureEntry
{
    /// <summary>
    /// The key to look the member up by: for a
    /// <see cref="UiStructureMemberKind.Item"/> the key it is filed under in
    /// <see cref="Elements.UiSectionContainerElement.Items"/>, and for a
    /// <see cref="UiStructureMemberKind.Action"/> the key it is filed under in
    /// <see cref="Elements.UiSectionContainerElement.Actions"/>, which is the
    /// action's <see cref="UiAction.ID"/>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Which of the container's two maps <see cref="Name"/> points into.
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
}
