using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   The permission required to invoke an executable action. Orthogonal to
///   <see cref="ActionScope"/> — an action of any scope may require either
///   permission level.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ActionPermission
{
    /// <summary>
    ///   Only administrators may invoke the action.
    /// </summary>
    Admin,

    /// <summary>
    ///   Any authenticated user may invoke the action.
    /// </summary>
    User,
}
