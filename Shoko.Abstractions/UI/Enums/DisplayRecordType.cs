using System.Runtime.Serialization;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Types of records in the UI for a record field/property.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DisplayRecordType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    [EnumMember(Value = "auto")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("auto")]
    Auto = 0,

    /// <summary>
    /// A dropdown record where you select each existing entry in a drop down.
    /// Only usable by complex record types.
    /// </summary>
    [EnumMember(Value = "complex-dropdown")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("complex-dropdown")]
    ComplexDropdown = 1,

    /// <summary>
    /// A tab record where you select each existing entry in as a tab.
    /// Only usable by complex record types.
    /// </summary>
    [EnumMember(Value = "complex-tab")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("complex-tab")]
    ComplexTab = 2,
}
