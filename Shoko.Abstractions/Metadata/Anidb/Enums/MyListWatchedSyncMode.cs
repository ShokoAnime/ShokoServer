using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   How to resolve watched-state conflicts during a MyList sync when the
///   entry was updated on the same day as the local watch. Older
///   differences are governed by the individual read/set settings instead.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MyListWatchedSyncMode : byte
{
    /// <summary>
    ///   Leaves same-day watched-state differences alone.
    /// </summary>
    [Display(Name = "Ignore")]
    Ignore = 0,

    /// <summary>
    ///   Exports the local watched state to AniDB.
    /// </summary>
    [Display(Name = "Trust Local")]
    TrustLocal = 1,

    /// <summary>
    ///   Imports the watched state from AniDB to the local library.
    /// </summary>
    [Display(Name = "Trust Remote")]
    TrustRemote = 2,
}
