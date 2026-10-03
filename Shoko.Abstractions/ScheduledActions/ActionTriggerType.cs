using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   What makes a scheduled action run on its own.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ActionTriggerType
{
    /// <summary>
    ///   Runs once the interval has passed since the action last ran, with no
    ///   alignment to the clock.
    /// </summary>
    Interval,

    /// <summary>
    ///   Runs every day at a time of day, in the server's time zone.
    /// </summary>
    Daily,

    /// <summary>
    ///   Runs every week on some days at a time of day, in the server's time
    ///   zone.
    /// </summary>
    Weekly,

    /// <summary>
    ///   Runs every time the server has started.
    /// </summary>
    Startup,

    /// <summary>
    ///   Runs every month on some days, counted from the start or the end of
    ///   the month, at a time of day, in the server's time zone.
    /// </summary>
    Monthly,

    /// <summary>
    ///   Runs every time the job queue is cleared, by an admin, a plugin or
    ///   other code. Not when the queue runs out of jobs, nor when it is paused
    ///   or resumed.
    /// </summary>
    QueueCleared,
}
