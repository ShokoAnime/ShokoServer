
using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Types of selects in the UI for a select field/property.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplaySelectType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    [EnumMember(Value = "auto")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("auto")]
    Auto = 0,

    /// <summary>
    /// A flat list where all the options are viewed at once.
    /// </summary>
    [EnumMember(Value = "flat-list")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("flat-list")]
    FlatList = 1,

    /// <summary>
    /// A flat list where all the options are viewed at once, with checkboxes for each option.
    /// </summary>
    [EnumMember(Value = "checkbox-list")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("checkbox-list")]
    CheckboxList = 2,
}
