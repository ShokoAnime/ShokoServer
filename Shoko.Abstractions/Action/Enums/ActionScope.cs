using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Action.Enums;

/// <summary>
///   The scope of an action.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ActionScope : byte
{
    /// <summary>
    ///   Global administrative action. Requires administrator privileges.
    /// </summary>
    Global = 1,

    /// <summary>
    ///   Global user action. Can be ran by any user.
    /// </summary>
    GlobalUser = 2,

    /// <summary>
    ///   Group-level action. Requires administrator privileges.
    /// </summary>
    Group = 3,

    /// <summary>
    ///   Group-level user action. Can be ran by any user.
    /// </summary>
    GroupUser = 4,

    /// <summary>
    ///   Series-level action. Requires administrator privileges.
    /// </summary>
    Series = 5,

    /// <summary>
    ///   Series-level user action. Can be ran by any user.
    /// </summary>
    SeriesUser = 6,

    /// <summary>
    ///   Episode-level action. Requires administrator privileges.
    /// </summary>
    Episode = 7,

    /// <summary>
    ///   Episode-level user action. Can be ran by any user.
    /// </summary>
    EpisodeUser = 8,
}
