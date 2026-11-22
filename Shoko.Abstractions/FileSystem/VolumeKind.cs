using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What kind of storage a <see cref="VolumeStat"/> is on.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum VolumeKind : byte
{
    /// <summary>
    ///   Another kind, or one the platform does not tell, such as an overlay
    ///   or a pseudo file system.
    /// </summary>
    Generic = 0,

    /// <summary>
    ///   A local disk that stays in.
    /// </summary>
    Fixed = 1,

    /// <summary>
    ///   A local disk that can be taken out, such as a USB stick or a card.
    /// </summary>
    Removable = 2,

    /// <summary>
    ///   A share on another machine (SMB, NFS and the like).
    /// </summary>
    Network = 3,

    /// <summary>
    ///   An optical disc.
    /// </summary>
    Optical = 4,

    /// <summary>
    ///   Memory, such as a tmpfs or a RAM disk; gone at the next start.
    /// </summary>
    Memory = 5,
}
