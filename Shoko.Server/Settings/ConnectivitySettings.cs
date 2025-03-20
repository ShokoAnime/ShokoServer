
using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config.Attributes;

namespace Shoko.Server.Settings;

public class ConnectivitySettings
{
    /// <summary>
    /// Disabled connectivity monitor services.
    /// </summary>
    [EnvironmentVariable("SHOKO_DISABLED_MONITOR_SERVICES")]
    public HashSet<string> DisabledMonitorServices { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);
}
