using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   The storage state of an AniDB MyList entry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MylistState : byte
{
    /// <summary>
    ///   The storage state is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   The file is stored on a hard drive.
    /// </summary>
    HDD = 1,

    /// <summary>
    ///   The file is stored on an optical disc.
    /// </summary>
    Disk = 2,

    /// <summary>
    ///   The file has been deleted.
    /// </summary>
    Deleted = 3,

    /// <summary>
    ///   The file is stored remotely.
    /// </summary>
    Remote = 4,
}
