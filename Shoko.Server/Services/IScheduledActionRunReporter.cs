using System;

namespace Shoko.Server.Services;

/// <summary>
/// Told by the queue job of a scheduled action's run when the action starts
/// and ends, so the run events can be raised.
/// </summary>
public interface IScheduledActionRunReporter
{
    /// <summary>
    /// Raises the run started event for the action.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <returns>When the run started, in UTC.</returns>
    DateTime ReportRunStarted(Guid actionId);

    /// <summary>
    /// Raises the run completed event for the action.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="startedAt">When the run started, in UTC.</param>
    /// <param name="exception">What the action threw, or <c>null</c> when it ended well.</param>
    void ReportRunCompleted(Guid actionId, DateTime startedAt, Exception? exception);
}
