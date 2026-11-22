using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Core;

/// <summary>
///   The core's HTTP APIs, which an admin can turn on and off.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum CoreApis : byte
{
    /// <summary>
    ///   None of them.
    /// </summary>
    None = 0,

    /// <summary>
    ///   The legacy APIv2, under <c>/api/v2</c> and the version-less routes it
    ///   kept.
    /// </summary>
    V2 = 1,

    /// <summary>
    ///   APIv3, under <c>/api/v3</c>.
    /// </summary>
    V3 = 2,
}
