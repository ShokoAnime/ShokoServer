using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.API.v3.Models.Action;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when scheduled actions came, went or changed while the server
/// runs.
/// </summary>
/// <param name="args">The event arguments.</param>
public class ScheduledActionsChangedSignalRModel(ScheduledActionsChangedEventArgs args)
{
    /// <summary>
    /// The actions added, as they are now.
    /// </summary>
    public IReadOnlyList<ScheduledAction> Added { get; } = [.. args.Added.Select(ScheduledAction.FromScheduledActionInfo)];

    /// <summary>
    /// The actions whose name, description or defaults changed, as they are now.
    /// </summary>
    public IReadOnlyList<ScheduledAction> Updated { get; } = [.. args.Updated.Select(ScheduledAction.FromScheduledActionInfo)];

    /// <summary>
    /// The actions removed, as they were.
    /// </summary>
    public IReadOnlyList<ScheduledAction> Removed { get; } = [.. args.Removed.Select(ScheduledAction.FromScheduledActionInfo)];
}
