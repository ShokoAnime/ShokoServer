using System;
using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class ReleaseProviderSettings
{
    public bool ParallelMode { get; set; } = false;

    public Dictionary<Guid, bool> Enabled { get; set; } = [];

    public List<Guid> Priority { get; set; } = [];
}
