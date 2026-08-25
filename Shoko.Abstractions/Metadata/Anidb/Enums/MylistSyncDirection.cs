using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   Which side of the sync a step writes to. The distinction a plan-only run cares
///   about most: whether approving a step changes the local library or the
///   user's MyList on AniDB.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MylistSyncDirection : byte
{
    /// <summary>
    ///   Writes nothing anywhere. A step that would leave the entry exactly as
    ///   it already is has no side to write to.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Writes local data, taking AniDB as the source.
    /// </summary>
    Import = 1,

    /// <summary>
    ///   Writes the MyList on AniDB, taking the local library as the source.
    /// </summary>
    Export = 2,
}
