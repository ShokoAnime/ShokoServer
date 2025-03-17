using System.Runtime.Serialization;

namespace Shoko.Plugin.Abstractions.Config.Enums;

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
    [EnumMember(Value = "checkbox")]
    Checkbox = 1,

    /// <summary>
    /// A comma separated list.
    /// Only usable by simple list types.
    /// </summary>
    [EnumMember(Value = "comma")]
    Comma = 2,

    /// <summary>
    /// A dropdown list where you select each existing entry in a drop down.
    /// Only usable by complex list types.
    /// </summary>
    [EnumMember(Value = "dropdown")]
    Dropdown = 3,

    /// <summary>
    /// A tab list where you select each existing entry in as a tab.
    /// Only usable by complex list types.
    /// </summary>
    [EnumMember(Value = "tab")]
    Tab = 4,
}
