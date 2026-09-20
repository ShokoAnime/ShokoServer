using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Determines the color theme used for an element in the UI.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayColorTheme
{
    /// <summary>
    /// The element is displayed as a default themed element in the UI.
    /// </summary>
    [EnumMember(Value = "default")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("default")]
    Default = 0,

    /// <summary>
    /// The element is displayed as a primary themed element in the UI.
    /// </summary>
    [EnumMember(Value = "primary")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("primary")]
    Primary = 1,

    /// <summary>
    /// The element is displayed as a secondary themed element in the UI.
    /// </summary>
    [EnumMember(Value = "secondary")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("secondary")]
    Secondary = 2,

    /// <summary>
    /// The element is displayed as an important themed element in the UI.
    /// </summary>
    [EnumMember(Value = "important")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("important")]
    Important = 3,

    /// <summary>
    /// The element is displayed as a warning themed element in the UI.
    /// </summary>
    [EnumMember(Value = "warning")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("warning")]
    Warning = 4,

    /// <summary>
    /// The element is displayed as a danger themed element in the UI.
    /// </summary>
    [EnumMember(Value = "danger")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("danger")]
    Danger = 5,
}
