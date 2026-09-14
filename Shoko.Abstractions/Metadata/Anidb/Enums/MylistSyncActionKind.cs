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

    /// <summary>
    ///   Nothing to do. The sync looked at this and found nothing to do,
    ///   because what it would have written is already there.
    ///
    ///   Left out of a plan unless
    ///   <see cref="Models.MylistSyncOptions.IncludeNoOperations"/> asks for
    ///   it, so that a plan reads as the work a sync will do rather than as a
    ///   census of everything it looked at. Applying one does nothing either
    ///   way, so a plan means the same thing whether or not they are in it.
    /// </summary>
    NoOperation = 5,
}
