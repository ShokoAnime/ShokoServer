
namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Plugin settings provider.
/// </summary>
public interface ISettingsProvider
{
    /// <summary>
    /// Save the plugin settings.
    /// </summary>
    /// <param name="settings">Settings to save.</param>
    void SaveSettings(IPluginSettings settings);
}
