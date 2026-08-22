using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Types of sections in the UI for a class/group.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplaySectionType
{
    /// <summary>
    /// The sections is displayed as a field-set in the UI.
    /// </summary>
    [EnumMember(Value = "field-set")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("field-set")]
    FieldSet = 0,

    /// <summary>
    /// The sections is displayed as a set of tabs in the UI.
    /// </summary>
    [EnumMember(Value = "tab")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("tab")]
    Tab = 1,

    /// <summary>
    /// The sections is displayed with simple headers in the UI.
    /// </summary>
    [EnumMember(Value = "minimal")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("minimal")]
    Minimal = 2,

    /// <summary>
    /// The sections is displayed as a checkbox list in the UI.
    /// </summary>
    [EnumMember(Value = "checkbox")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("checkbox")]
    Checkbox = 3,
}
