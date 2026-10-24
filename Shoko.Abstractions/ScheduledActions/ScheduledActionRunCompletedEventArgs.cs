using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Dispatched when a scheduled action's run ends, well or not.
/// </summary>
public class ScheduledActionRunCompletedEventArgs : EventArgs
{
    /// <summary>
    ///   The scheduled action, as it is when the run ends.
    /// </summary>
    public required ScheduledActionInfo Action { get; init; }

    /// <summary>
    ///   When the run started, in UTC.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    ///   When the run ended, in UTC.
    /// </summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    ///   What the action threw, or <c>null</c> when it ended well.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    ///   Whether the run stopped because it was cancelled.
    /// </summary>
    public bool IsCancelled => Exception is OperationCanceledException;

    /// <summary>
    ///   The API token of whoever queued the run, or <c>null</c> when a
    ///   trigger or the system did. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
