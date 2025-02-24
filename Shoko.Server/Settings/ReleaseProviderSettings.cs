using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class ReleaseProviderSettings
{
    public bool ParallelMode { get; set; } = false;

    public Dictionary<string, bool> Enabled { get; set; } = [];

    public List<string> Priority { get; set; } = [];
}
