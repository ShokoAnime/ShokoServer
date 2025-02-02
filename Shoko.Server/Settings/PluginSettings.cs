using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class PluginSettings
{
    public Dictionary<string, bool> EnabledPlugins { get; set; } = [];

    public List<string> Priority { get; set; } = [];

    public RenamerSettings Renamer { get; set; } = new();

    public ReleaseProviderSettings ReleaseProvider { get; set; } = new();
}
