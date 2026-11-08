using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Plugin.Enums;

/// <summary>
///   Where a <see cref="Models.ThirdPartyNotice"/>'s package or asset comes from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ThirdPartyNoticeSource : byte
{
    /// <summary>
    ///   A source the notices file names that the server does not know.
    /// </summary>
    Generic = 0,

    /// <summary>
    ///   A NuGet package.
    /// </summary>
    NuGet = 1,

    /// <summary>
    ///   An npm package.
    /// </summary>
    Npm = 2,

    /// <summary>
    ///   An asset no package carries, such as a font.
    /// </summary>
    Asset = 3,

    /// <summary>
    ///   The plugin itself, for its own licence.
    /// </summary>
    Project = 4,
}
