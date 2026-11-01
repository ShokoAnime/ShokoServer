using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   What an AniDB message is about, as far as the server can act on it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AnidbMessageKind : byte
{
    /// <summary>
    ///   A message the server has no handling for.
    /// </summary>
    Generic = 0,

    /// <summary>
    ///   AniDB moved a file to another anime or episode; handling it searches
    ///   for the file's release again.
    /// </summary>
    FileMoved = 1,
}
