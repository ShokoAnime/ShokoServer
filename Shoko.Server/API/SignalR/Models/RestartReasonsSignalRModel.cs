using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.API.v3.Models.Shoko;

using AbstractRestartReason = Shoko.Abstractions.Core.RestartReason;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Every reason the server needs a restart, sent when an admin joins the <c>restart</c> feed and
/// whenever the reasons change.
/// </summary>
/// <param name="reasons">The reasons that stand.</param>
/// <param name="pluginManager">The plugin manager, to name the plugins.</param>
public class RestartReasonsSignalRModel(IReadOnlyList<AbstractRestartReason> reasons, IPluginManager pluginManager)
{
    /// <summary>
    /// Indicates that at least one reason stands.
    /// </summary>
    public bool RestartRequired { get; init; } = reasons.Count > 0;

    /// <summary>
    /// Every reason that stands, oldest first.
    /// </summary>
    public List<RestartReason> Reasons { get; init; } = reasons.Select(reason => new RestartReason(reason, pluginManager)).ToList();
}
