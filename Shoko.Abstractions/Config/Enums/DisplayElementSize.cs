using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// Determines the size of an element in the UI.
/// </summary>
public enum DisplayElementSize
{
    /// <summary>
    /// The element will span it's default size in the UI.
    /// </summary>
    [EnumMember(Value = "normal")]
    Normal = 0,

    /// <summary>
    /// The element will span less the default size in the UI.
    /// </summary>
    [EnumMember(Value = "small")]
    Small = 1,

    /// <summary>
    /// The element will span more the default size in the UI.
    /// </summary>
    [EnumMember(Value = "large")]
    Large = 2,

    /// <summary>
    /// The element will span full size of it's container in the UI.
    /// </summary>
    [EnumMember(Value = "full")]
    Full = 3,
}
