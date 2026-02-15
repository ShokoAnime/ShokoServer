using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

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
    /// The element is placed at the start at the parent component.
    /// </summary>
    [EnumMember(Value = "start")]
    Start = 1,

    /// <summary>
    /// Alias for Start.
    /// </summary>
    Top = Start,

    /// <summary>
    /// Alias for Start.
    /// </summary>
    Left = Start,

    /// <summary>
    /// The element is placed at the end of the parent component.
    /// </summary>
    [EnumMember(Value = "end")]
    End = 2,

    /// <summary>
    /// Alias for End.
    /// </summary>
    Right = End,

    /// <summary>
    /// Alias for End.
    /// </summary>
    Bottom = End,
}
