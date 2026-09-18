using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What an airing schedule releases. A provider declares the kinds it can
///   supply, and the service rejects tracks of any other kind.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AiringKind : byte
{
    /// <summary>
    ///   The original-language broadcast or release.
    /// </summary>
    Original = 0,

    /// <summary>
    ///   A subtitled release.
    /// </summary>
    Subtitled = 1,

    /// <summary>
    ///   A dubbed release.
    /// </summary>
    Dubbed = 2,
}
