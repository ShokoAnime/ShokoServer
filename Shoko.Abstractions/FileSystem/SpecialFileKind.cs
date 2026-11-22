using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   What kind of special file a <see cref="SpecialFileStat"/> is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum SpecialFileKind : byte
{
    /// <summary>
    ///   Another kind than those below.
    /// </summary>
    Generic = 0,

    /// <summary>
    ///   A named pipe (FIFO).
    /// </summary>
    Pipe = 1,

    /// <summary>
    ///   A Unix domain socket.
    /// </summary>
    Socket = 2,

    /// <summary>
    ///   A character device, such as a terminal.
    /// </summary>
    CharacterDevice = 3,

    /// <summary>
    ///   A block device, such as a disk.
    /// </summary>
    BlockDevice = 4,
}
