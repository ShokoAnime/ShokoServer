using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Where a log level rule comes from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum LogLevelRuleSource : byte
{
    /// <summary>
    ///   One of the core's defaults.
    /// </summary>
    Core = 0,

    /// <summary>
    ///   A default a plugin registered.
    /// </summary>
    Plugin = 1,

    /// <summary>
    ///   A custom rule from the logging settings.
    /// </summary>
    Custom = 2,
}
