using System;
using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class HashingProviderSettings
{
    public bool ParallelMode { get; set; } = false;

    public Dictionary<Guid, HashSet<string>> EnabledHashes { get; set; } = [];

    public List<Guid> Priority { get; set; } = [];
}
