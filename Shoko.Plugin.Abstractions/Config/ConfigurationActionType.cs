
namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Types of configuration actions that can be performed on a configuration.
/// </summary>
public enum ConfigurationActionType
{
    /// <summary>
    /// A custom action is being performed on the configuration.
    /// </summary>
    Custom = 0,

    /// <summary>
    /// The configuration is about to be saved by this action.
    /// </summary>
    Saved = 1,

    /// <summary>
    /// The configuration has just been loaded into the UI.
    /// </summary>
    Loaded = 2,

    /// <summary>
    /// The configuration was changed in the UI without being saved.
    /// </summary>
    Changed = 3,
}
