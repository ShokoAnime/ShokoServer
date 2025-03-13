
namespace Shoko.Plugin.Abstractions.Config.Enums;

/// <summary>
/// Types of lists in the UI for a list field/property.
/// </summary>
public enum DisplayListType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// A list where all the options are viewed at once, with checkboxes for each option.
    /// </summary>
    Checkbox = 1,

    /// <summary>
    /// A dropdown list where you select each existing entry in a drop down.
    /// Only usable by complex list types.
    /// </summary>
    Dropdown = 2,
}
