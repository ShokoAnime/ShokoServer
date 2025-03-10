
namespace Shoko.Plugin.Abstractions.Config.Enums;

/// <summary>
/// The visibility of a configuration property/field in the UI.
/// </summary>
public enum DisplayVisibility
{
    /// <summary>
    /// The property/field is visible in the UI.
    /// </summary>
    Visible = 0,

    /// <summary>
    /// The property/field is hidden in the UI.
    /// </summary>
    Hidden = 1,

    /// <summary>
    /// The property/field is marked as read-only in the UI.
    /// </summary>
    ReadOnly = 2,

    /// <summary>
    /// The property/field is marked as disabled in the UI.
    /// </summary>
    Disabled = 3,
}
