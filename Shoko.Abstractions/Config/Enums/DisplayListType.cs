using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Types of lists in the UI for a list field/property.
/// </summary>
public enum DisplayListType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto = 0,

    /// <summary>
    /// A list where all the options are viewed at once, with checkboxes for each option.
    /// </summary>
    [EnumMember(Value = "enum-checkbox")]
    EnumCheckbox = 1,

    /// <summary>
    /// A dropdown list where you select each existing entry in a drop down.
    /// Only usable by complex list types.
    /// </summary>
    [EnumMember(Value = "complex-dropdown")]
    ComplexDropdown = 2,

    /// <summary>
    /// A tab list where you select each existing entry in as a tab.
    /// Only usable by complex list types.
    /// </summary>
    [EnumMember(Value = "complex-tab")]
    ComplexTab = 3,

    /// <summary>
    /// A complex list where each entry is showed inline as only the name,
    /// optionally, with actions per entry.
    /// </summary>
    [EnumMember(Value = "complex-inline")]
    ComplexInline = 4,
}
