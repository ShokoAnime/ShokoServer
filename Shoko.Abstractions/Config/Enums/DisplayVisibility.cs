using System.Runtime.Serialization;

namespace Shoko.Abstractions.Config.Enums;

/// <summary>
/// The visibility of a configuration property/field in the UI.
/// </summary>
public enum DisplayVisibility
{
    /// <summary>
    /// The property/field is visible in the UI.
    /// </summary>
    [EnumMember(Value = "visible")]
    Visible = 0,

    /// <summary>
    /// The property/field is hidden in the UI.
    /// </summary>
    [EnumMember(Value = "hidden")]
    Hidden = 1,

    /// <summary>
    /// The property/field is marked as read-only in the UI.
    /// </summary>
    [EnumMember(Value = "read-only")]
    ReadOnly = 2,
}
