using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.ReleaseExporter;

public class Plugin : IPlugin
{
    public Guid ID => Guid.Parse("df4ea747-98e6-587a-be14-3a71f9403a3e");

    public string Name => "Release Importer/Exporter";

    public void Load() { }
}
