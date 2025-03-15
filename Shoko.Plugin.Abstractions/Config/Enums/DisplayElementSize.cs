using System.Runtime.Serialization;

namespace Shoko.Plugin.Abstractions.Config.Enums;

/// <summary>
/// Determines the size of an element in the UI.
/// </summary>
public enum DisplayElementSize
{
    /// <summary>
    /// The element will span it's default size in the UI.
    /// </summary>
    [EnumMember(Value = "default")]
    Default = 0,

    /// <summary>
    /// The element will span half the default size in the UI.
    /// </summary>
    [EnumMember(Value = "half")]
    Half = 1,

    /// <summary>
    /// The element will span double the default size in the UI.
    /// </summary>
    [EnumMember(Value = "double")]
    Double = 2,

    /// <summary>
    /// The element will span full size of it's container in the UI.
    /// </summary>
    [EnumMember(Value = "full")]
    Full = 3,
}
