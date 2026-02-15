
using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Types of selects in the UI for a select field/property.
/// </summary>
public enum DisplaySelectType
{
    /// <summary>
    /// Auto behavior based on complexity and type.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto = 0,

    /// <summary>
    /// A flat list where all the options are viewed at once.
    /// </summary>
    [EnumMember(Value = "flat-list")]
    FlatList = 1,

    /// <summary>
    /// A flat list where all the options are viewed at once, with checkboxes for each option.
    /// </summary>
    [EnumMember(Value = "checkbox-list")]
    CheckboxList = 2,
}
