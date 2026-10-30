using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   What a <see cref="Models.PluginChangePlan"/> is for.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PluginChangeKind
{
    /// <summary>
    ///   Uninstalling a plugin.
    /// </summary>
    Uninstall = 0,

    /// <summary>
    ///   Disabling a plugin.
    /// </summary>
    Disable = 1,
}
