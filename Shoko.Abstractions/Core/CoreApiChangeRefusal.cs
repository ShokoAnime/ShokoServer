using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Core;

/// <summary>
///   Why a core API could not be turned on or off.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum CoreApiChangeRefusal : byte
{
    /// <summary>
    ///   Another reason than those below.
    /// </summary>
    Generic = 0,

    /// <summary>
    ///   A web UI in use needs it, so it cannot be turned off until another
    ///   web UI is picked.
    /// </summary>
    NeededByWebUI = 1,
}
