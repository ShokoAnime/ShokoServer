using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.RelocationPlus;

public class Plugin : IPlugin
{
    public Guid ID => Guid.Parse("96d412cf-6013-5154-b696-2b95b545d360");

    public string Name => "Relocation+";

    public void Load() { }
}
