using System.Runtime.Serialization;

namespace Shoko.Plugin.Abstractions.Config.Enums;

/// <summary>
/// Position of an element in the UI.
/// </summary>
public enum DisplayButtonPosition
{
    /// <summary>
    /// The element is automatically placed.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto = 0,

    /// <summary>
    /// The element is placed at the top.
    /// </summary>
    [EnumMember(Value = "top")]
    Top = 1,

    /// <summary>
    /// The element is placed at the bottom.
    /// </summary>
    [EnumMember(Value = "bottom")]
    Bottom = 2,
}
