using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Action.Enums;

/// <summary>
///   The scope of an action.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ActionScope : byte
{
    /// <summary>
    ///   Indicates the scope is ran at the system-level.
    /// </summary>
    System = 1 << 0,

    /// <summary>
    ///   Indicates the scope is ran at the user-level, per user.
    /// </summary>
    User = 1 << 1,

    /// <summary>
    ///   Global-scoped action. The scoped action is not particularly tied to
    ///   any particular group, series or episode.
    /// </summary>
    Global = 1 << 2,

    /// <summary>
    ///   Global system-level administrative action. Requires administrator
    ///   privileges to run.
    /// </summary>
    SystemAndGlobal = Global | System,

    /// <summary>
    ///   Global user-level user action. Can be ran by any user.
    /// </summary>
    UserAndGlobal = Global | User,

    /// <summary>
    ///   Group-scoped action. The action is scope to run on a group.
    /// </summary>
    Group = 1 << 3,

    /// <summary>
    ///   Group-scoped system-level administrative action. Requires
    ///   administrator privileges to run.
    /// </summary>
    SystemAndGroup = System | Group,

    /// <summary>
    ///   Group-scoped user-level user action. Can be ran by any user.
    /// </summary>
    UserAndGroup = User | Group,

    /// <summary>
    ///   Series-scoped action. The action is scope to run on a series.
    /// </summary>
    Series = 1 << 4,

    /// <summary>
    ///   Series-scoped system-level administrative action. Requires
    ///   administrator privileges to run.
    /// </summary>
    SystemAndSeries = System | Series,

    /// <summary>
    ///   Series-scoped user-level user action. Can be ran by any user.
    /// </summary>
    UserAndSeries = User | Series,

    /// <summary>
    ///   Episode-scoped action. The action is scope to run on an episode.
    /// </summary>
    Episode = 1 << 5,

    /// <summary>
    ///   Episode-scoped system-level administrative action. Requires
    ///   administrator privileges to run.
    /// </summary>
    SystemAndEpisode = System | Episode,

    /// <summary>
    ///   Episode-scoped user-level user action. Can be ran by any user.
    /// </summary>
    UserAndEpisode = User | Episode,
}
