using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Types of lists in the UI for a list field/property.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayListType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    [EnumMember(Value = "auto")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("auto")]
    Auto = 0,

    /// <summary>
    /// A list where all the options are viewed at once, with checkboxes for each option.
    /// </summary>
    [EnumMember(Value = "enum-checkbox")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("enum-checkbox")]
    EnumCheckbox = 1,

    /// <summary>
    /// A dropdown list where you select each existing entry in a drop down.
    /// Only usable by complex list types.
    /// </summary>
    [EnumMember(Value = "complex-dropdown")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("complex-dropdown")]
    ComplexDropdown = 2,

    /// <summary>
    /// A tab list where you select each existing entry in as a tab.
    /// Only usable by complex list types.
    /// </summary>
    [EnumMember(Value = "complex-tab")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("complex-tab")]
    ComplexTab = 3,

    /// <summary>
    /// A complex list where each entry is showed inline as only the name,
    /// optionally, with actions per entry.
    /// </summary>
    [EnumMember(Value = "complex-inline")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("complex-inline")]
    ComplexInline = 4,
}
