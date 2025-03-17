using System.Runtime.Serialization;

namespace Shoko.Plugin.Abstractions.Config.Enums;

/// <summary>
/// Types of sections in the UI for a class/group.
/// </summary>
public enum DisplaySectionType
{
    /// <summary>
    /// The sections is displayed as a field-set in the UI.
    /// </summary>
    [EnumMember(Value = "field-set")]
    FieldSet = 0,

    /// <summary>
    /// The sections is displayed as a set of tabs in the UI.
    /// </summary>
    [EnumMember(Value = "tab")]
    Tab = 1,

    /// <summary>
    /// The sections is displayed with simple headers in the UI.
    /// </summary>
    [EnumMember(Value = "minimal")]
    Minimal = 2,
}
