using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Web.Enums;

/// <summary>
///   The outcome of installing a web UI release.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum WebUIInstallResult : byte
{
    /// <summary>
    ///   The release is installed and served.
    /// </summary>
    Installed = 1,

    /// <summary>
    ///   The release is installed, but only served after a restart.
    /// </summary>
    RestartRequired = 2,

    /// <summary>
    ///   The release could not be installed. The installed one is kept.
    /// </summary>
    Failed = 3,
}
