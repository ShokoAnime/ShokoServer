using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.RelocationPlus;

public class Plugin : IPlugin
{
    public string Name => "Relocation+";

    public void Load() { }

    public void OnSettingsLoaded(IPluginSettings settings) { }
}
