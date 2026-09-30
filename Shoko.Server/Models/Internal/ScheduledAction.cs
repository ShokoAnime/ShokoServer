using System;

#nullable enable
namespace Shoko.Server.Models.Internal;

/// <summary>
/// The schedule of one global action: the triggers the admin set, and when it
/// last ran, by any means and by a trigger. One row per action the scheduler
/// has seen.
/// </summary>
public class ScheduledAction
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int ScheduledActionID { get; set; }

    /// <summary>
    /// The ID of the action.
    /// </summary>
    public Guid ActionID { get; set; }

    /// <summary>
    /// The triggers the admin set, as JSON, or <c>null</c> when the action's
    /// own defaults are in effect. An empty list means it never runs on its own.
    /// </summary>
    public string? Triggers { get; set; }

    /// <summary>
    /// When the action was last queued, by a trigger or by hand, or its run's
    /// queue job last started, whichever is later, in UTC, or <c>null</c> when
    /// it never was.
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// When a trigger last queued the action, or the queue job of a run a
    /// trigger queued last started, whichever is later, in UTC, or <c>null</c>
    /// when no trigger ever did. What the triggers count from, unless the
    /// action counts its runs by hand too.
    /// </summary>
    public DateTime? LastScheduledRunAt { get; set; }

    /// <summary>
    /// When the scheduler first saw the action, in UTC. What its triggers count
    /// from until it has run, so a server restarted more often than an interval
    /// still runs the action.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #endregion
}
