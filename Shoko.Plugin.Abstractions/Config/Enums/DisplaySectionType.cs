
namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Types of sections in the UI for a class/group.
/// </summary>
public enum DisplaySectionType
{
    /// <summary>
    /// The sections is displayed as a field set in the UI.
    /// </summary>
    FieldSet = 0,

    /// <summary>
    /// The sections is displayed as a set of tabs in the UI.
    /// </summary>
    Tab = 1,
}
