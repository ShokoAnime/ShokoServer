using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   Who a <see cref="Models.PluginFeature"/> is advertised to. Each level
///   also includes every level below it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PluginFeatureVisibility : byte
{
    /// <summary>
    ///   Advertised to every client, including unauthenticated ones. Anything
    ///   in the feature's metadata is visible to them too.
    /// </summary>
    Anonymous = 1,

    /// <summary>
    ///   Advertised to authenticated users.
    /// </summary>
    Authenticated = 2,

    /// <summary>
    ///   Advertised to administrators only.
    /// </summary>
    Admin = 3,
}
