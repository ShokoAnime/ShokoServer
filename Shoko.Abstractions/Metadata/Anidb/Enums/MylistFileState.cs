using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   The file state of an AniDB MyList entry.
/// </summary>
/// <remarks>
///   AniDB's UI splits this across two fields sharing one numeric space: the
///   <c>Type</c> of a real file (<see cref="Normal"/>, <see cref="Corrupted"/>,
///   <see cref="SelfEdited"/>, <see cref="Streamed"/>, <see cref="Other"/>) and
///   the <c>Generic Type</c> of a generic entry (<see cref="SelfRipped"/>
///   through <see cref="OnBluRay"/>, plus <see cref="Other"/>). The UDP API
///   exposes only one <c>filestate</c> parameter for both, and reads back
///   whichever applies to the entry. A write is validated against the UDP
///   definition's own list, which predates the Blu-ray option and never gained
///   it, so <see cref="OnBluRay"/> can be read but not written. Which of the
///   two columns an accepted write lands in is inconsistent; do not depend
///   on it.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MylistFileState : byte
{
    /// <summary>
    ///   The file is in a normal state.
    /// </summary>
    Normal = 0,

    /// <summary>
    ///   The file is corrupted.
    /// </summary>
    Corrupted = 1,

    /// <summary>
    ///   The file has been self-edited.
    /// </summary>
    SelfEdited = 2,

    /// <summary>
    ///   The file was self-ripped.
    /// </summary>
    SelfRipped = 10,

    /// <summary>
    ///   The file is stored on DVD.
    /// </summary>
    OnDVD = 11,

    /// <summary>
    ///   The file is stored on VHS.
    /// </summary>
    OnVHS = 12,

    /// <summary>
    ///   The file was recorded from TV.
    /// </summary>
    OnTV = 13,

    /// <summary>
    ///   The file was recorded in theaters.
    /// </summary>
    InTheaters = 14,

    /// <summary>
    ///   The file was streamed.
    /// </summary>
    Streamed = 15,

    /// <summary>
    ///   The file is stored on Blu-ray. Generic entries only. Readable, but not
    ///   writable over the UDP API, whose validator predates the option and
    ///   answers <c>505 ILLEGAL INPUT OR ACCESS DENIED</c> for it.
    /// </summary>
    OnBluRay = 16,

    /// <summary>
    ///   The file has another state.
    /// </summary>
    Other = 100,
}
