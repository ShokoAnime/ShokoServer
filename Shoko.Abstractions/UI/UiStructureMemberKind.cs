using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI;

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
