using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   The entity level an executable action is bound to.
/// </summary>
/// <remarks>
///   <para>
///     Global actions implement <see cref="IExecutableAction"/> directly.
///     Scoped actions derive from one of the three base classes
///     (<see cref="SeriesAction"/>, <see cref="GroupAction"/>,
///     <see cref="EpisodeAction"/>), which fix the scope at compile time.
///   </para>
///   <para>
///     Actions available for a given scope do not vary by <em>which</em>
///     series/group/episode is being viewed, only by which entity type it is,
///     so listings are scope-filtered, not entity-filtered.
///   </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ActionScope
{
    /// <summary>
    ///   The action is not tied to a particular entity.
    /// </summary>
    Global,

    /// <summary>
    ///   The action operates on a group.
    /// </summary>
    Group,

    /// <summary>
    ///   The action operates on a series.
    /// </summary>
    Series,

    /// <summary>
    ///   The action operates on an episode.
    /// </summary>
    Episode,
}
