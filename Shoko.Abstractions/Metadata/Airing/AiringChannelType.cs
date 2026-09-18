using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What kind of channel something airs on. The type is part of a channel's
///   identity, so the same name with another type is another channel.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AiringChannelType : byte
{
    /// <summary>
    ///   Catch-all for channels whose kind is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   A broadcast station, e.g. TOKYO MX or BS11.
    /// </summary>
    Television = 1,

    /// <summary>
    ///   A streaming service, e.g. Crunchyroll or Amazon (US).
    /// </summary>
    Streaming = 2,
}
