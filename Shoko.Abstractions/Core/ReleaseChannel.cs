
namespace Shoko.Abstractions.Core;

/// <summary>
/// The release channel of the currently running server.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum ReleaseChannel
{
    /// <summary>
    /// Automatically determine the release channel based on the current version.
    /// </summary>
    Auto = -1,

    /// <summary>
    /// The debug channel.
    /// </summary>
    Debug = 0,

    /// <summary>
    /// The stable channel.
    /// </summary>
    Stable = 1,

    /// <summary>
    /// The dev channel.
    /// </summary>
    Dev = 2,
}
