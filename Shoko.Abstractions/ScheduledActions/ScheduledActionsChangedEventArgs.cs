using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Dispatched when scheduled actions came, went or changed while the server
///   runs, as an <see cref="IScheduledActionProvider"/> changed its actions.
/// </summary>
public class ScheduledActionsChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The actions added, as they are now.
    /// </summary>
    public required IReadOnlyList<ScheduledActionInfo> Added { get; init; }

    /// <summary>
    ///   The actions whose name, description or declared defaults changed, as
    ///   they are now.
    /// </summary>
    public required IReadOnlyList<ScheduledActionInfo> Updated { get; init; }

    /// <summary>
    ///   The actions removed, as they were.
    /// </summary>
    public required IReadOnlyList<ScheduledActionInfo> Removed { get; init; }
}
