using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Position of an element in the UI.
/// </summary>
/// <remarks>
/// Every member carries a distinct value. Which name a serializer hands back
/// for a value two members share is unspecified, so the enum has no aliases.
/// </remarks>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayButtonPosition
{
    /// <summary>
    /// The element is automatically placed.
    /// </summary>
    [EnumMember(Value = "auto")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("auto")]
    Auto = 0,

    /// <summary>
    /// The element is placed at the start of the parent component: its top
    /// edge or its leading edge, depending on how the parent lays out.
    /// </summary>
    [EnumMember(Value = "start")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("start")]
    Start = 1,

    /// <summary>
    /// The element is placed at the end of the parent component: its bottom
    /// edge or its trailing edge, depending on how the parent lays out.
    /// </summary>
    [EnumMember(Value = "end")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("end")]
    End = 2,
}
