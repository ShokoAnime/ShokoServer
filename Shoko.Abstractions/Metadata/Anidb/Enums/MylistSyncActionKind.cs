using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   A single thing a MyList sync does, or would do. Each is named for the
///   direction it moves data in, since that is what decides whether it changes
///   the local library or the MyList on AniDB.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MylistSyncActionKind : byte
{
    /// <summary>
    ///   Write the watched state AniDB holds onto the local video or episode.
    ///   The only kind that changes local data rather than the MyList.
    /// </summary>
    ImportWatchedState = 1,

    /// <summary>
    ///   Send the local watched state, storage state, or both to an entry
    ///   AniDB already holds. The mirror of
    ///   <see cref="ImportWatchedState"/>.
    /// </summary>
    ExportWatchedState = 2,

    /// <summary>
    ///   Add an entry AniDB does not hold yet.
    /// </summary>
    ExportEntryAddition = 3,

    /// <summary>
    ///   Dispose of an entry, which the delete type may turn into a state
    ///   change rather than an outright removal.
    /// </summary>
    ExportEntryRemoval = 4,
}
