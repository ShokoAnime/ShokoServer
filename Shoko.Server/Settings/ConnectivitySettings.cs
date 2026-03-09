using System.Collections.Generic;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

#nullable enable
namespace Shoko.Server.Settings;

public class ConnectivitySettings
{
    /// <summary>
    /// The list of connectivity monitor definitions used for WAN availability checks.
    /// When <c>null</c>, the built-in defaults are used.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    public List<ConnectivityMonitorDefinition>? MonitorDefinitions { get; set; }
}
