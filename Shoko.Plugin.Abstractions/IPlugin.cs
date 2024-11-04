
namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Interface for plugins to register themselves automagically.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Friendly name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Load event.
    /// </summary>
    void Load();

    /// <summary>
    /// This will be called with the created settings object if you have an <see cref="IPluginSettings"/> in the Plugin.
    /// You can cast to your desired type and set the settings within it.
    /// </summary>
    /// <param name="settings"></param>
    void OnSettingsLoaded(IPluginSettings settings);
}
