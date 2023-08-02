
using System;
using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class ConnectivitySettings
{
    /// <summary>
    /// Disabled connectivity monitor services.
    /// </summary>
    public HashSet<string> DisabledMonitorServices { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);
}
