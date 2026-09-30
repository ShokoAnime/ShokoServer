using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Where the queue job of a scheduled action's run is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ScheduledActionState
{
    /// <summary>
    ///   Not in the queue.
    /// </summary>
    Idle,

    /// <summary>
    ///   Waiting in the queue.
    /// </summary>
    Waiting,

    /// <summary>
    ///   Running.
    /// </summary>
    Running,

    /// <summary>
    ///   Running, and asked to stop. It stays like this until it stops, and
    ///   the request cannot be undone.
    /// </summary>
    CancellationRequested,
}
