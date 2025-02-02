using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class ReleaseProviderSettings
{
    public Dictionary<string, bool> Enabled { get; set; } = [];

    public List<string> Priority { get; set; } = [];
}
