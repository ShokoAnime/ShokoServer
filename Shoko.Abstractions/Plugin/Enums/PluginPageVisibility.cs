using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   Who a <see cref="Models.PluginPage"/> is offered to. Each level also
///   includes every level below it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PluginPageVisibility : byte
{
    /// <summary>
    ///   Offered to authenticated users.
    /// </summary>
    Authenticated = 1,

    /// <summary>
    ///   Offered to administrators only.
    /// </summary>
    Admin = 2,
}
