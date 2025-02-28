using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the
/// core to register plugin providers. You cannot
/// disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    /// <inheritdoc/>
    public string Name => "Shoko Core";

    /// <inheritdoc/>
    public void Load() { }

    /// <inheritdoc/>
    public void OnSettingsLoaded(IPluginSettings settings)
    {
        // No settings!? Maybe make this abstract in the future
    }
}
