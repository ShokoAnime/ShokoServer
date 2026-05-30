
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Seasons of the year.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum YearlySeason : byte
{
    /// <summary>
    /// Winter.
    /// </summary>
    Winter = 0,

    /// <summary>
    /// Spring.
    /// </summary>
    Spring = 1,

    /// <summary>
    /// Summer.
    /// </summary>
    Summer = 2,

    /// <summary>
    /// Autumn/Fall.
    /// </summary>
    Fall = 3,

    /// <summary>
    /// Autumn/Fall. (Alias)
    /// </summary>
    Autumn = Fall,
}
