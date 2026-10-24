using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Dispatched when the queue job of a scheduled action's run starts the
///   action, once the action agreed to run.
/// </summary>
public class ScheduledActionRunStartedEventArgs : EventArgs
{
    /// <summary>
    ///   The scheduled action, as it is when the run starts.
    /// </summary>
    public required ScheduledActionInfo Action { get; init; }

    /// <summary>
    ///   When the run started, in UTC.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    ///   The API token of whoever queued the run, or <c>null</c> when a
    ///   trigger or the system did. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
