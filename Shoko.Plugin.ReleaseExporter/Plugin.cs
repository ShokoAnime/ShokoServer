using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.ReleaseExporter;

public class Plugin : IPlugin
{
    public string Name => "Release Exporter";

    public void Load() { }

    public void OnSettingsLoaded(IPluginSettings settings) { }
}
