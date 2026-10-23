using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Dispatched when the triggers of a scheduled action are set or reset.
/// </summary>
public class ScheduledActionTriggersChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The scheduled action as it was before the change.
    /// </summary>
    public required ScheduledActionInfo Previous { get; init; }

    /// <summary>
    ///   The scheduled action as it is after the change.
    /// </summary>
    public required ScheduledActionInfo Current { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
