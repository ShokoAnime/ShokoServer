using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   Who sent an AniDB message, as AniDB marks it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AnidbMessageType : byte
{
    /// <summary>
    ///   A message from another user.
    /// </summary>
    Normal = 0,

    /// <summary>
    ///   A message from another user who chose not to be named.
    /// </summary>
    Anonymous = 1,

    /// <summary>
    ///   A message from AniDB itself, such as a file being moved.
    /// </summary>
    System = 2,

    /// <summary>
    ///   A message from an AniDB moderator.
    /// </summary>
    Moderator = 3,
}
