using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   What happens to a plugin version in a <see cref="Models.PluginChangePlan"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PluginChange
{
    /// <summary>
    ///   Nothing: it stays as it is.
    /// </summary>
    None = 0,

    /// <summary>
    ///   It is uninstalled.
    /// </summary>
    Uninstall = 1,

    /// <summary>
    ///   It is disabled.
    /// </summary>
    Disable = 2,

    /// <summary>
    ///   It stays enabled but does not load from the next start, as a
    ///   required dependency is gone.
    /// </summary>
    StopLoading = 3,
}
