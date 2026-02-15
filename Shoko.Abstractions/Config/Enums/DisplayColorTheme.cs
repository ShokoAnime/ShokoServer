using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Determines the color theme used for an element in the UI.
/// </summary>
public enum DisplayColorTheme
{
    /// <summary>
    /// The element is displayed as a default themed element in the UI.
    /// </summary>
    [EnumMember(Value = "default")]
    Default = 0,

    /// <summary>
    /// The element is displayed as a primary themed element in the UI.
    /// </summary>
    [EnumMember(Value = "primary")]
    Primary = 1,

    /// <summary>
    /// The element is displayed as a secondary themed element in the UI.
    /// </summary>
    [EnumMember(Value = "secondary")]
    Secondary = 2,

    /// <summary>
    /// The element is displayed as an important themed element in the UI.
    /// </summary>
    [EnumMember(Value = "important")]
    Important = 3,

    /// <summary>
    /// The element is displayed as a warning themed element in the UI.
    /// </summary>
    [EnumMember(Value = "warning")]
    Warning = 4,

    /// <summary>
    /// The element is displayed as a danger themed element in the UI.
    /// </summary>
    [EnumMember(Value = "danger")]
    Danger = 5,
}
