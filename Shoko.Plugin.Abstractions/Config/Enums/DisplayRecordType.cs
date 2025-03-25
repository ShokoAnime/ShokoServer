using System.Runtime.Serialization;

namespace Shoko.Plugin.Abstractions.Config.Enums;

/// <summary>
/// Types of records in the UI for a record field/property.
/// </summary>
public enum DisplayRecordType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto = 0,

    /// <summary>
    /// A dropdown record where you select each existing entry in a drop down.
    /// Only usable by complex record types.
    /// </summary>
    [EnumMember(Value = "complex-dropdown")]
    ComplexDropdown = 1,

    /// <summary>
    /// A tab record where you select each existing entry in as a tab.
    /// Only usable by complex record types.
    /// </summary>
    [EnumMember(Value = "complex-tab")]
    ComplexTab = 2,
}
