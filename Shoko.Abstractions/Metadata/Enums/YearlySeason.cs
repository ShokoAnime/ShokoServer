
namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Seasons of the year.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
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
