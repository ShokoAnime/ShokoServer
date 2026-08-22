using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// The visibility of a property/field in the UI.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayVisibility
{
    /// <summary>
    /// The property/field is visible in the UI.
    /// </summary>
    [EnumMember(Value = "visible")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("visible")]
    Visible = 0,

    /// <summary>
    /// The property/field is hidden in the UI.
    /// </summary>
    [EnumMember(Value = "hidden")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("hidden")]
    Hidden = 1,

    /// <summary>
    /// The property/field is marked as read-only in the UI.
    /// </summary>
    [EnumMember(Value = "read-only")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("read-only")]
    ReadOnly = 2,
}
