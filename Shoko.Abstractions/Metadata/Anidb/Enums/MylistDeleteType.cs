using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   How to remove an entry from the MyList when the local file is gone.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MylistDeleteType : byte
{
    /// <summary>
    ///   Delete the entry from the MyList.
    /// </summary>
    Delete = 0,

    /// <summary>
    ///   Keep the entry on the MyList, only delete from the local database.
    /// </summary>
    DeleteLocalOnly = 1,

    /// <summary>
    ///   Mark the entry as deleted.
    /// </summary>
    MarkDeleted = 2,

    /// <summary>
    ///   Mark the entry as stored on external storage.
    /// </summary>
    MarkExternalStorage = 3,

    /// <summary>
    ///   Mark the entry as having an unknown storage state.
    /// </summary>
    MarkUnknown = 4,

    /// <summary>
    ///   Mark the entry as stored on disk.
    /// </summary>
    MarkDisk = 5,
}
